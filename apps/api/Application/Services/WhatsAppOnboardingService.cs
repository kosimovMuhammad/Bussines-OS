using System.Data.Common;
using System.Security.Cryptography;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Services;

public class WhatsAppOnboardingService(
    IAppDbContext db,
    IWhatsAppClient client,
    ITokenCipher tokenCipher,
    IOAuthStateService oauthState,
    IAuditLogService auditLog,
    IOptions<WhatsAppPlatformOptions> options,
    ILogger<WhatsAppOnboardingService> logger) : IWhatsAppOnboardingService
{
    // Embedded Signup version the frontend must request (extras.version). v5 turns WhatsApp
    // Business app onboarding (Coexistence) on by default and reports messaging_account_ids.
    public const string SignupVersion = "v5";

    private const string FinishEvent = "FINISH";
    private const string FinishBusinessAppEvent = "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING";
    private const string FinishOnlyWabaEvent = "FINISH_ONLY_WABA";
    private const string FinishGrantOnlyEvent = "FINISH_GRANT_ONLY_API_ACCESS";

    private const string ManagementScope = "whatsapp_business_management";
    private const string MessagingScope = "whatsapp_business_messaging";
    private const string ConnectedStatus = "CONNECTED";

    public WhatsAppEmbeddedSignupConfigDto GetConfig(string userId, string companyId, string? locale)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.AppId) || string.IsNullOrWhiteSpace(o.EmbeddedSignupConfigurationId))
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.NotConfigured);
        }

        return new WhatsAppEmbeddedSignupConfigDto(
            o.AppId, o.EmbeddedSignupConfigurationId, o.ApiVersion, SignupVersion,
            oauthState.Create(userId, companyId, AppLocales.Normalize(locale)));
    }

    public async Task<WhatsAppConnectResultDto> CompleteAsync(
        string companyId, string userId, CompleteWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await CompleteCoreAsync(companyId, userId, request, cancellationToken);
        }
        catch (WhatsAppOnboardingException ex)
        {
            // The exception middleware does not log 4xx/5xx onboarding errors; this is the one place that
            // records WHY a connect attempt failed (stable code only - never the code, token or PIN).
            logger.LogWarning("WhatsApp onboarding FAILED: code={ErrorCode} http={Status} company={CompanyId} user={UserId} event={Event}",
                ex.Code, ex.StatusCode, companyId, userId, request.Session?.Event);
            throw;
        }
    }

    private async Task<WhatsAppConnectResultDto> CompleteCoreAsync(
        string companyId, string userId, CompleteWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken)
    {
        // 1. The state must be ours, fresh, and minted for THIS user in THIS company.
        var state = oauthState.Validate(request.State);
        if (state is null || state.UserId != userId || state.CompanyId != companyId)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.InvalidState);
        }

        var session = request.Session;
        logger.LogInformation(
            "WhatsApp onboarding [1/7] state valid: company={CompanyId} user={UserId} event={Event} hintedPhoneNumberId={HintedPhoneNumberId}",
            companyId, userId, request.Session?.Event, request.Session?.PhoneNumberId);
        var signupEvent = session?.Event;
        if (signupEvent == FinishOnlyWabaEvent)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.NoPhoneNumber);
        }

        if (signupEvent == FinishGrantOnlyEvent)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.UnsupportedFlow);
        }

        if (signupEvent is not null && signupEvent is not (FinishEvent or FinishBusinessAppEvent))
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.InvalidSession);
        }

        // 2. Exchange the code first - it lives ~30 seconds. The result is the customer's business
        //    token. We deliberately do NOT run fb_exchange_token on it: Meta documents the Embedded
        //    Signup result as a business token and never mentions that grant for it, so its real
        //    expiry is read from debug_token below instead of being assumed.
        string accessToken;
        try
        {
            accessToken = (await client.ExchangeCodeAsync(request.Code, cancellationToken)).AccessToken;
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "WhatsApp Embedded Signup code exchange failed for company {CompanyId}", companyId);
            throw WhatsAppOnboardingErrors.FromProvider(ex, WhatsAppOnboardingErrors.CodeExchangeFailed);
        }

        logger.LogInformation("WhatsApp onboarding [2/7] one-time code exchanged for a business token (company={CompanyId})", companyId);

        // 3. Ask Meta what this token can actually reach. Nothing the browser reported is trusted.
        var tokenInfo = await CallProviderAsync(() => client.DebugTokenAsync(accessToken, cancellationToken), WhatsAppOnboardingErrors.ProviderError);
        var expectedAppId = options.Value.AppId;
        if (!tokenInfo.IsValid || (!string.IsNullOrEmpty(expectedAppId) && tokenInfo.AppId != expectedAppId))
        {
            logger.LogWarning("WhatsApp Embedded Signup token rejected for company {CompanyId}: valid={IsValid} appIdMatches={AppIdMatches}",
                companyId, tokenInfo.IsValid, tokenInfo.AppId == expectedAppId);
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.InvalidToken);
        }

        var grantedWabaIds = tokenInfo.TargetIdsFor(ManagementScope).Union(tokenInfo.TargetIdsFor(MessagingScope)).ToList();
        if (grantedWabaIds.Count == 0)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.NoWabaAccess);
        }

        logger.LogInformation(
            "WhatsApp onboarding [3/7] token verified: fbUserId={FbUserId} expiresAt={ExpiresAt} grantedWabaIds={GrantedWabaIds} scopes={Scopes}",
            tokenInfo.UserId, tokenInfo.ExpiresAt?.ToString("O") ?? "never", string.Join(",", grantedWabaIds),
            string.Join(",", tokenInfo.GranularScopes.Select(s => s.Scope)));

        // 4. Resolve the WABA(s): browser hints may only NARROW the granted set, never extend it.
        var hintedWabaIds = (session?.MessagingAccountIds ?? [])
            .Append(session?.WabaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        if (hintedWabaIds.Any(id => !grantedWabaIds.Contains(id)))
        {
            logger.LogWarning("WhatsApp Embedded Signup for company {CompanyId} reported a WABA the token cannot access", companyId);
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.AssetMismatch);
        }

        var candidateWabaIds = hintedWabaIds.Count > 0 ? hintedWabaIds : grantedWabaIds;
        var numbers = new List<(string WabaId, WhatsAppPhoneNumberInfo Info)>();
        foreach (var wabaId in candidateWabaIds)
        {
            var wabaNumbers = await CallProviderAsync(() => client.GetWabaPhoneNumbersAsync(accessToken, wabaId, cancellationToken), WhatsAppOnboardingErrors.ProviderError);
            numbers.AddRange(wabaNumbers.Select(n => (wabaId, n)));
        }

        (string WabaId, WhatsAppPhoneNumberInfo Info) chosen;
        if (!string.IsNullOrWhiteSpace(session?.PhoneNumberId))
        {
            var match = numbers.Where(n => n.Info.Id == session.PhoneNumberId).ToList();
            if (match.Count == 0)
            {
                logger.LogWarning("WhatsApp Embedded Signup for company {CompanyId} reported a phone number the token cannot access", companyId);
                throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.AssetMismatch);
            }

            chosen = match[0];
        }
        else if (numbers.Count == 1)
        {
            chosen = numbers[0];
        }
        else
        {
            throw WhatsAppOnboardingErrors.Create(numbers.Count == 0 ? WhatsAppOnboardingErrors.NoPhoneNumber : WhatsAppOnboardingErrors.AmbiguousPhoneNumber);
        }

        // Authoritative details for the chosen number (is_on_biz_app is what decides register-or-not);
        // fall back to the listing if this extra call fails.
        var details = chosen.Info;
        try
        {
            details = await client.GetPhoneNumberAsync(accessToken, chosen.Info.Id, cancellationToken);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "WhatsApp phone number details lookup failed for {PhoneNumberId}; using the listing", chosen.Info.Id);
        }

        var isOnBizApp = details.IsOnBizApp ?? session?.IsOnBizApp ?? signupEvent == FinishBusinessAppEvent;

        logger.LogInformation(
            "WhatsApp onboarding [4/7] number resolved: phoneNumberId={PhoneNumberId} wabaId={WabaId} isOnBizApp={IsOnBizApp} (meta={MetaFlag}) platformType={PlatformType} status={Status}",
            details.Id, chosen.WabaId, isOnBizApp, details.IsOnBizApp?.ToString() ?? "omitted", details.PlatformType, details.Status);

        // 5. A Meta phone number belongs to exactly one tenant. Check BEFORE touching Meta (register /
        //    subscribe) so a number owned by another company is never modified.
        var takenByAnotherCompany = await db.WhatsAppAccounts.IgnoreQueryFilters()
            .AnyAsync(a => a.PhoneNumberId == details.Id && a.CompanyId != companyId, cancellationToken);
        if (takenByAnotherCompany)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.AlreadyConnected);
        }

        var existing = await db.WhatsAppAccounts.SingleOrDefaultAsync(
            a => a.CompanyId == companyId && a.PhoneNumberId == details.Id, cancellationToken);

        // 6. Post-signup steps. Failures here do not lose the connection: the account is saved as
        //    NeedsAttention with the failed steps reported, and retry re-runs them.
        var issues = new List<string>();
        var pinCipher = await RegisterIfNeededAsync(accessToken, details.Id, isOnBizApp, existing?.EncryptedRegistrationPin, issues, cancellationToken);
        await SubscribeAsync(accessToken, chosen.WabaId, issues, cancellationToken);

        // 7. Save.
        var account = existing;
        if (account is null)
        {
            account = new WhatsAppAccount { CompanyId = companyId, PhoneNumberId = details.Id };
            db.WhatsAppAccounts.Add(account);
        }

        account.WabaId = chosen.WabaId;
        account.EncryptedAccessToken = tokenCipher.Encrypt(accessToken);
        account.TokenExpiresAt = tokenInfo.ExpiresAt;
        account.VerifiedName = details.VerifiedName ?? account.VerifiedName;
        account.DisplayPhoneNumber = details.DisplayPhoneNumber ?? account.DisplayPhoneNumber;
        account.BusinessId = string.IsNullOrWhiteSpace(session?.BusinessId) ? account.BusinessId : session.BusinessId;
        account.ConnectedByFbUserId = tokenInfo.UserId ?? account.ConnectedByFbUserId;
        account.IsOnBizApp = isOnBizApp;
        account.EncryptedRegistrationPin = isOnBizApp ? null : pinCipher;
        account.ConnectionStatus = issues.Count == 0 ? WhatsAppConnectionStatus.Active : WhatsAppConnectionStatus.NeedsAttention;
        account.HistorySyncStatus = NextHistorySyncStatus(isOnBizApp, account.HistorySyncStatus);
        account.OnboardedAt = DateTime.UtcNow;
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        auditLog.Log(companyId, userId, "whatsapp_account.connected", "WhatsAppAccount", account.Id,
            new { account.PhoneNumberId, account.WabaId, account.IsOnBizApp, Issues = issues });

        await SaveAsync(cancellationToken);
        logger.LogInformation(
            "WhatsApp onboarding [7/7] saved: accountId={AccountId} phoneNumberId={PhoneNumberId} status={Status} isOnBizApp={IsOnBizApp} historySync={HistorySync} issues=[{Issues}]",
            account.Id, account.PhoneNumberId, account.ConnectionStatus, account.IsOnBizApp, account.HistorySyncStatus, string.Join(",", issues));
        return new WhatsAppConnectResultDto(WhatsAppService.ToDto(account), issues);
    }

    public async Task<WhatsAppConnectResultDto> RetryAsync(string companyId, string userId, string accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.WhatsAppAccounts.SingleOrDefaultAsync(a => a.Id == accountId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("WhatsApp account not found");

        string accessToken;
        try
        {
            accessToken = tokenCipher.Decrypt(account.EncryptedAccessToken);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.InvalidToken);
        }

        var issues = new List<string>();
        var needsRegister = false;
        if (!account.IsOnBizApp)
        {
            try
            {
                var info = await client.GetPhoneNumberAsync(accessToken, account.PhoneNumberId, cancellationToken);
                needsRegister = !string.Equals(info.Status, ConnectedStatus, StringComparison.OrdinalIgnoreCase);
            }
            catch (ExternalServiceException)
            {
                // Can't tell - register is the safer default for a number that is not yet working.
                needsRegister = true;
            }
        }

        if (needsRegister)
        {
            account.EncryptedRegistrationPin = await RegisterIfNeededAsync(
                accessToken, account.PhoneNumberId, false, account.EncryptedRegistrationPin, issues, cancellationToken);
        }

        await SubscribeAsync(accessToken, account.WabaId, issues, cancellationToken);

        account.ConnectionStatus = issues.Count == 0 ? WhatsAppConnectionStatus.Active : WhatsAppConnectionStatus.NeedsAttention;
        account.UpdatedAt = DateTime.UtcNow;
        auditLog.Log(companyId, userId, "whatsapp_account.retried", "WhatsAppAccount", account.Id, new { Issues = issues });

        await db.SaveChangesAsync(cancellationToken);
        return new WhatsAppConnectResultDto(WhatsAppService.ToDto(account), issues);
    }

    public WhatsAppSignupClientEventResultDto ReportClientEvent(string companyId, string userId, WhatsAppSignupClientEventRequest request)
    {
        var code = request.Event == "CANCEL"
            ? WhatsAppOnboardingErrors.Cancelled
            : string.IsNullOrWhiteSpace(request.ErrorCode)
                ? WhatsAppOnboardingErrors.Unknown
                : WhatsAppOnboardingErrors.FromMetaCode(request.ErrorCode.Trim());

        // Meta support asks for the session id; the free-text error message is deliberately not logged.
        logger.LogWarning(
            "WhatsApp Embedded Signup popup {Event}: company={CompanyId} user={UserId} step={Step} metaCode={MetaCode} mapped={Mapped} sessionId={SessionId}",
            request.Event, companyId, userId, request.CurrentStep, request.ErrorCode, code, request.SessionId);

        return new WhatsAppSignupClientEventResultDto(code, WhatsAppOnboardingErrors.MessageFor(code));
    }

    // Standard numbers must be registered (with a 6-digit two-step PIN) before they can send;
    // Coexistence numbers are already registered, so this is skipped for them. Returns the encrypted
    // PIN to store. Meta allows 10 register calls / number / 72h, so this never loops.
    private async Task<string?> RegisterIfNeededAsync(
        string accessToken, string phoneNumberId, bool isOnBizApp, string? existingEncryptedPin, List<string> issues, CancellationToken cancellationToken)
    {
        if (isOnBizApp)
        {
            logger.LogInformation("WhatsApp onboarding [5/7] register skipped for {PhoneNumberId}: Coexistence number is already registered", phoneNumberId);
            return null;
        }

        string? pin = null;
        if (existingEncryptedPin is not null)
        {
            try
            {
                pin = tokenCipher.Decrypt(existingEncryptedPin);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                logger.LogWarning("Stored WhatsApp registration PIN for {PhoneNumberId} could not be decrypted; generating a new one", phoneNumberId);
            }
        }

        pin ??= RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        try
        {
            await client.RegisterPhoneNumberAsync(accessToken, phoneNumberId, pin, cancellationToken);
            logger.LogInformation("WhatsApp onboarding [5/7] registered {PhoneNumberId} for Cloud API", phoneNumberId);
            return tokenCipher.Encrypt(pin);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "WhatsApp register failed for {PhoneNumberId}", phoneNumberId);
            issues.Add(WhatsAppOnboardingErrors.FromProvider(ex, WhatsAppOnboardingErrors.RegisterFailed).Code);
            return existingEncryptedPin;
        }
    }

    private async Task SubscribeAsync(string accessToken, string wabaId, List<string> issues, CancellationToken cancellationToken)
    {
        try
        {
            await client.SubscribeWebhookAsync(accessToken, wabaId, cancellationToken);
            logger.LogInformation("WhatsApp onboarding [6/7] app subscribed to WABA {WabaId}", wabaId);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "WhatsApp webhook subscription failed for WABA {WabaId}", wabaId);
            issues.Add(WhatsAppOnboardingErrors.FromProvider(ex, WhatsAppOnboardingErrors.SubscribeFailed).Code);
        }
    }

    // The one-time contacts + history sync can only be requested once (and within 24h of onboarding);
    // never reset a sync that was already requested/finished when the same number reconnects.
    private static WhatsAppHistorySyncStatus NextHistorySyncStatus(bool isOnBizApp, WhatsAppHistorySyncStatus current)
    {
        if (!isOnBizApp)
        {
            return WhatsAppHistorySyncStatus.NotApplicable;
        }

        return current is WhatsAppHistorySyncStatus.Requested or WhatsAppHistorySyncStatus.Completed or WhatsAppHistorySyncStatus.Declined
            ? current
            : WhatsAppHistorySyncStatus.Pending;
    }

    private static async Task<T> CallProviderAsync<T>(Func<Task<T>> call, string fallbackCode)
    {
        try
        {
            return await call();
        }
        catch (ExternalServiceException ex)
        {
            throw WhatsAppOnboardingErrors.FromProvider(ex, fallbackCode);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: "23505" })
        {
            // Lost a race with another company claiming the same globally unique PhoneNumberId.
            throw WhatsAppOnboardingErrors.Create(WhatsAppOnboardingErrors.AlreadyConnected);
        }
    }
}
