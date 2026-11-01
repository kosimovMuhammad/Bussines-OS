using BusinessOS.Application.Configurations;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusinessOS.Tests.Unit;

public class WhatsAppOnboardingTests
{
    private const string AppId = "app-1";
    private const string CoexistenceEvent = "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING";

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; } = new();
        public FakeWhatsAppClient Client { get; } = new();
        public RoundTripOAuthStateService State { get; } = new();
        public FakeTokenCipher Cipher { get; } = new();
        public WhatsAppOnboardingService Service { get; }
        public Company Company { get; }
        public User User { get; }

        public Fixture(string configurationId = "cfg-1")
        {
            Company = new Company { Name = "Acme" };
            Db.Context.Companies.Add(Company);
            Db.Context.SaveChanges();
            User = AddUser(Company.Id);

            Service = new WhatsAppOnboardingService(
                Db.Context, Client, Cipher, State, new AuditLogService(Db.Context),
                Options.Create(new WhatsAppPlatformOptions { AppId = AppId, EmbeddedSignupConfigurationId = configurationId }),
                NullLogger<WhatsAppOnboardingService>.Instance);

            // Defaults: a valid token for this app that can reach waba-1 only.
            Client.NextTokenInfo = TokenInfo("waba-1");
        }

        public User AddUser(string companyId)
        {
            var user = new User { CompanyId = companyId, Name = "U", Email = $"{Guid.NewGuid():N}@acme.tj" };
            Db.Context.Users.Add(user);
            Db.Context.SaveChanges();
            return user;
        }

        public string ValidState() => State.Create(User.Id, Company.Id);

        public CompleteWhatsAppEmbeddedSignupRequest Request(WhatsAppSignupSessionInfo? session, string? state = null) =>
            new("code-1", state ?? ValidState(), session);

        public Task<WhatsAppConnectResultDto> Complete(WhatsAppSignupSessionInfo? session, string? state = null) =>
            Service.CompleteAsync(Company.Id, User.Id, Request(session, state));

        public void Dispose() => Db.Dispose();
    }

    private static WhatsAppTokenInfo TokenInfo(string wabaId, string appId = AppId, bool valid = true, DateTime? expiresAt = null) =>
        new(valid, appId, "fb-user-1", expiresAt,
        [
            new WhatsAppGranularScope("whatsapp_business_management", [wabaId]),
            new WhatsAppGranularScope("whatsapp_business_messaging", [wabaId]),
        ]);

    private static WhatsAppPhoneNumberInfo Number(string id, bool? onBizApp, string status = "CONNECTED") =>
        new(id, "+992 90 000 0001", "Shop", "CLOUD_API", onBizApp, status);

    private static WhatsAppSignupSessionInfo Session(string? ev = "FINISH", string? phone = null, string? waba = null, bool? biz = null, string? business = null) =>
        new(ev, phone, waba, null, business, biz);

    private static async Task<WhatsAppOnboardingException> Fails(Task task)
    {
        try
        {
            await task;
        }
        catch (WhatsAppOnboardingException ex)
        {
            return ex;
        }

        throw new Xunit.Sdk.XunitException("Expected a WhatsAppOnboardingException");
    }

    // ---- config ------------------------------------------------------------------------------

    [Fact]
    public void GetConfig_ReturnsAppConfigVersionsAndAStateBoundToTheUser()
    {
        using var f = new Fixture();

        var config = f.Service.GetConfig(f.User.Id, f.Company.Id, "ru");

        Assert.Equal((AppId, "cfg-1", "v26.0", "v5"), (config.AppId, config.ConfigurationId, config.GraphVersion, config.SignupVersion));
        var payload = f.State.Validate(config.State)!;
        Assert.Equal((f.User.Id, f.Company.Id, "ru"), (payload.UserId, payload.CompanyId, payload.Locale));
    }

    [Fact]
    public void GetConfig_NormalizesUnknownLocale()
    {
        using var f = new Fixture();
        Assert.Equal("en", f.State.Validate(f.Service.GetConfig(f.User.Id, f.Company.Id, "../evil").State)!.Locale);
    }

    [Fact]
    public async Task GetConfig_WithoutConfigurationId_IsNotConfigured503()
    {
        using var f = new Fixture(configurationId: "");
        var ex = await Fails(Task.Run(() => f.Service.GetConfig(f.User.Id, f.Company.Id, null)));
        Assert.Equal(("not_configured", 503), (ex.Code, ex.StatusCode));
    }

    // ---- coexistence (the first live test) ----------------------------------------------------

    [Fact]
    public async Task Coexistence_AcceptsBusinessAppEvent_SkipsRegister_SubscribesAndStoresFlags()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];
        f.Client.NextTokenInfo = TokenInfo("waba-1", expiresAt: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await f.Complete(Session(CoexistenceEvent, phone: "pn-1", waba: "waba-1", business: "biz-1"));

        Assert.Empty(result.Issues);
        Assert.Empty(f.Client.Registered);
        Assert.Equal(["waba-1"], f.Client.SubscribedWabaIds);

        var account = Assert.Single(f.Db.Context.WhatsAppAccounts);
        Assert.Equal(("pn-1", "waba-1"), (account.PhoneNumberId, account.WabaId));
        Assert.True(account.IsOnBizApp);
        Assert.Null(account.EncryptedRegistrationPin);
        Assert.Equal(WhatsAppConnectionStatus.Active, account.ConnectionStatus);
        Assert.Equal(WhatsAppHistorySyncStatus.Pending, account.HistorySyncStatus);
        Assert.Equal("fb-user-1", account.ConnectedByFbUserId);
        Assert.Equal("biz-1", account.BusinessId);
        Assert.Equal("+992 90 000 0001", account.DisplayPhoneNumber);
        Assert.Equal("Shop", account.VerifiedName);
        Assert.Equal(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), account.TokenExpiresAt);
        Assert.NotNull(account.OnboardedAt);
        Assert.True(account.IsActive);
        Assert.True(result.Account.IsOnBizApp);
    }

    [Fact]
    public async Task Coexistence_FlagFromMetaWinsOverTheBrowserHint()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", false)];

        // Browser claims coexistence, Meta says it is a standard number -> register runs.
        await f.Complete(Session(CoexistenceEvent, phone: "pn-1", biz: true));

        Assert.Single(f.Client.Registered);
        Assert.False(f.Db.Context.WhatsAppAccounts.Single().IsOnBizApp);
    }

    [Fact]
    public async Task Coexistence_WhenMetaOmitsTheFlag_FallsBackToTheSessionEvent()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", null)];

        await f.Complete(Session(CoexistenceEvent, phone: "pn-1"));

        Assert.Empty(f.Client.Registered);
        Assert.True(f.Db.Context.WhatsAppAccounts.Single().IsOnBizApp);
    }

    [Fact]
    public async Task Coexistence_DoesNotRequestHistorySyncYet()
    {
        // RequestSmbAppDataSyncAsync throws in the fake: the one-time sync must wait for the
        // webhook processing that arrives in a later phase, or the data would be dropped.
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];

        await f.Complete(Session(CoexistenceEvent, phone: "pn-1"));

        Assert.Equal(WhatsAppHistorySyncStatus.Pending, f.Db.Context.WhatsAppAccounts.Single().HistorySyncStatus);
    }

    // ---- standard numbers ---------------------------------------------------------------------

    [Fact]
    public async Task Standard_RegistersWithAGeneratedSixDigitPin_AndStoresItEncrypted()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-2", false)];

        var result = await f.Complete(Session("FINISH", phone: "pn-2"));

        var (phoneNumberId, pin) = Assert.Single(f.Client.Registered);
        Assert.Equal("pn-2", phoneNumberId);
        Assert.Matches("^[0-9]{6}$", pin);

        var account = f.Db.Context.WhatsAppAccounts.Single();
        Assert.Equal(pin, f.Cipher.Decrypt(account.EncryptedRegistrationPin!));
        Assert.NotEqual(pin, account.EncryptedRegistrationPin);
        Assert.Equal(WhatsAppHistorySyncStatus.NotApplicable, account.HistorySyncStatus);
        Assert.Empty(result.Issues);
        Assert.Equal(WhatsAppConnectionStatus.Active, account.ConnectionStatus);
    }

    [Fact]
    public async Task TokenAndPinNeverAppearInTheReturnedAccount()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-2", false)];

        var result = await f.Complete(Session("FINISH", phone: "pn-2"));

        var account = f.Db.Context.WhatsAppAccounts.Single();
        Assert.NotEqual("business-token", account.EncryptedAccessToken);
        Assert.Equal("business-token", f.Cipher.Decrypt(account.EncryptedAccessToken));
        Assert.DoesNotContain(typeof(WhatsAppConnectResultDto).GetProperties().Concat(typeof(WhatsAppAccountDto).GetProperties()),
            p => p.Name.Contains("Pin") || (p.Name.Contains("Token") && p.Name != "TokenExpiresAt"));
        Assert.Equal("pn-2", result.Account.PhoneNumberId);
    }

    [Fact]
    public async Task Reconnect_UpdatesTheSameRow_ReusesStoredPin_AndKeepsAFinishedSync()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-2", false)];
        await f.Complete(Session("FINISH", phone: "pn-2"));
        var firstPin = f.Client.Registered.Single().Pin;

        var account = f.Db.Context.WhatsAppAccounts.Single();
        account.HistorySyncStatus = WhatsAppHistorySyncStatus.Completed;
        f.Db.Context.SaveChanges();

        await f.Complete(Session("FINISH", phone: "pn-2"));

        Assert.Single(f.Db.Context.WhatsAppAccounts);
        Assert.Equal([firstPin, firstPin], f.Client.Registered.Select(r => r.Pin));
    }

    // ---- server-side derivation: browser hints are never trusted -----------------------------

    [Fact]
    public async Task SpoofedWabaHint_IsRejected_AndNothingIsTouched()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];

        var ex = await Fails(f.Complete(Session(CoexistenceEvent, phone: "pn-1", waba: "someone-elses-waba")));

        Assert.Equal("asset_mismatch", ex.Code);
        Assert.Empty(f.Db.Context.WhatsAppAccounts);
        Assert.Empty(f.Client.SubscribedWabaIds);
        Assert.Empty(f.Client.Registered);
    }

    [Fact]
    public async Task SpoofedPhoneNumberHint_IsRejected()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];

        var ex = await Fails(f.Complete(Session("FINISH", phone: "victim-phone-number-id")));

        Assert.Equal("asset_mismatch", ex.Code);
        Assert.Empty(f.Db.Context.WhatsAppAccounts);
    }

    [Fact]
    public async Task Ids_AreDerivedFromTheToken_WhenTheBrowserSendsNone()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];

        await f.Complete(Session(CoexistenceEvent));

        Assert.Equal(("pn-1", "waba-1"), (f.Db.Context.WhatsAppAccounts.Single().PhoneNumberId, f.Db.Context.WhatsAppAccounts.Single().WabaId));
    }

    [Fact]
    public async Task V5MessagingAccountIds_AreAcceptedInsteadOfWabaId()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];
        var session = new WhatsAppSignupSessionInfo(CoexistenceEvent, "pn-1", null, ["waba-1"], "biz-1", true);

        await f.Complete(session);

        Assert.Equal("waba-1", f.Db.Context.WhatsAppAccounts.Single().WabaId);
    }

    [Fact]
    public async Task SeveralNumbersWithoutAHint_IsAmbiguous()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true), Number("pn-2", false)];
        Assert.Equal("ambiguous_phone_number", (await Fails(f.Complete(Session()))).Code);
    }

    [Fact]
    public async Task NoNumbersOnTheWaba_IsNoPhoneNumber()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [];
        Assert.Equal("no_phone_number", (await Fails(f.Complete(Session()))).Code);
    }

    [Fact]
    public async Task TokenWithoutWabaScopes_IsNoWabaAccess()
    {
        using var f = new Fixture();
        f.Client.NextTokenInfo = new WhatsAppTokenInfo(true, AppId, "u", null, [new WhatsAppGranularScope("business_management", ["biz"])]);
        Assert.Equal("no_waba_access", (await Fails(f.Complete(Session()))).Code);
    }

    // ---- state / token / flow validation ------------------------------------------------------

    [Fact]
    public async Task TamperedOrForeignState_IsRejected_BeforeAnyCodeExchange()
    {
        using var f = new Fixture();
        var otherUserState = f.State.Create("someone-else", f.Company.Id);
        var otherCompanyState = f.State.Create(f.User.Id, "another-company");

        foreach (var state in new[] { "garbage", otherUserState, otherCompanyState })
        {
            var ex = await Fails(f.Complete(Session(), state));
            Assert.Equal(("invalid_state", 400), (ex.Code, ex.StatusCode));
        }

        Assert.Empty(f.Client.ExchangedCodes);
    }

    [Theory]
    [InlineData("FINISH_ONLY_WABA", "no_phone_number")]
    [InlineData("FINISH_GRANT_ONLY_API_ACCESS", "unsupported_flow")]
    [InlineData("SOMETHING_NEW", "invalid_session")]
    public async Task UnusableFlowEvents_AreRejected_BeforeAnyCodeExchange(string signupEvent, string expectedCode)
    {
        using var f = new Fixture();

        var ex = await Fails(f.Complete(Session(signupEvent)));

        Assert.Equal(expectedCode, ex.Code);
        Assert.Empty(f.Client.ExchangedCodes);
    }

    [Fact]
    public async Task TokenMintedForAnotherApp_IsRejected()
    {
        using var f = new Fixture();
        f.Client.NextTokenInfo = TokenInfo("waba-1", appId: "some-other-app");
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];

        Assert.Equal("invalid_token", (await Fails(f.Complete(Session()))).Code);
        Assert.Empty(f.Db.Context.WhatsAppAccounts);
    }

    [Fact]
    public async Task InvalidToken_IsRejected()
    {
        using var f = new Fixture();
        f.Client.NextTokenInfo = TokenInfo("waba-1", valid: false);
        Assert.Equal("invalid_token", (await Fails(f.Complete(Session()))).Code);
    }

    [Fact]
    public async Task CodeExchangeFailure_MapsToCodeExchangeFailed()
    {
        using var f = new Fixture();
        f.Client.NextExchangeException = new WhatsAppApiException("This authorization code has expired", "100");

        var ex = await Fails(f.Complete(Session()));

        Assert.Equal(("code_exchange_failed", 502), (ex.Code, ex.StatusCode));
        Assert.Empty(f.Db.Context.WhatsAppAccounts);
    }

    [Fact]
    public async Task NumberOwnedByAnotherCompany_IsAConflict_AndMetaIsNotTouched()
    {
        using var f = new Fixture();
        var other = new Company { Name = "Other" };
        f.Db.Context.Companies.Add(other);
        f.Db.Context.WhatsAppAccounts.Add(new WhatsAppAccount
        {
            CompanyId = other.Id, PhoneNumberId = "pn-1", WabaId = "waba-1", EncryptedAccessToken = "enc:x",
        });
        f.Db.Context.SaveChanges();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", false)];

        var ex = await Fails(f.Complete(Session("FINISH", phone: "pn-1")));

        Assert.Equal(("already_connected", 409), (ex.Code, ex.StatusCode));
        Assert.Empty(f.Client.Registered);
        Assert.Empty(f.Client.SubscribedWabaIds);
        Assert.Equal(other.Id, f.Db.Context.WhatsAppAccounts.Single().CompanyId);
    }

    // ---- post-signup failures keep the connection, flag it, and are retryable ------------------

    [Fact]
    public async Task RegisterFailure_SavesNeedsAttention_WithTheMappedIssue()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-2", false)];
        f.Client.NextRegisterException = new WhatsAppApiException("too many", "133016");

        var result = await f.Complete(Session("FINISH", phone: "pn-2"));

        Assert.Equal(["register_rate_limited"], result.Issues);
        Assert.Equal(WhatsAppConnectionStatus.NeedsAttention, f.Db.Context.WhatsAppAccounts.Single().ConnectionStatus);
        Assert.Null(f.Db.Context.WhatsAppAccounts.Single().EncryptedRegistrationPin);
        Assert.Single(f.Client.SubscribedWabaIds);
    }

    [Fact]
    public async Task SubscribeFailure_SavesNeedsAttention_WithTheIssue()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];
        f.Client.NextSubscribeException = new WhatsAppApiException("denied", "200");

        var result = await f.Complete(Session(CoexistenceEvent, phone: "pn-1"));

        Assert.Equal(["subscribe_failed"], result.Issues);
        Assert.Equal(WhatsAppConnectionStatus.NeedsAttention, result.Account.ConnectionStatus);
    }

    [Fact]
    public async Task Retry_ReRegistersAndResubscribes_ThenBecomesActive()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-2", false, status: "PENDING")];
        f.Client.NextRegisterException = new WhatsAppApiException("boom", "1");
        await f.Complete(Session("FINISH", phone: "pn-2"));
        var accountId = f.Db.Context.WhatsAppAccounts.Single().Id;
        f.Client.NextRegisterException = null;

        var result = await f.Service.RetryAsync(f.Company.Id, f.User.Id, accountId);

        Assert.Empty(result.Issues);
        Assert.Equal(2, f.Client.Registered.Count);
        Assert.Equal(WhatsAppConnectionStatus.Active, f.Db.Context.WhatsAppAccounts.Single().ConnectionStatus);
        Assert.NotNull(f.Db.Context.WhatsAppAccounts.Single().EncryptedRegistrationPin);
    }

    [Fact]
    public async Task Retry_SkipsRegister_ForConnectedStandardNumbers_AndForCoexistenceNumbers()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true), Number("pn-2", false)];
        f.Client.NextSubscribeException = new WhatsAppApiException("x", "1");
        await f.Complete(Session(CoexistenceEvent, phone: "pn-1"));
        f.Client.NextSubscribeException = null;
        var coexistenceId = f.Db.Context.WhatsAppAccounts.Single().Id;

        var result = await f.Service.RetryAsync(f.Company.Id, f.User.Id, coexistenceId);

        Assert.Empty(result.Issues);
        Assert.Empty(f.Client.Registered);
        Assert.Equal(WhatsAppConnectionStatus.Active, result.Account.ConnectionStatus);
    }

    [Fact]
    public async Task Retry_OtherCompanysAccount_IsNotFound()
    {
        using var f = new Fixture();
        f.Client.PhoneNumbersByWaba["waba-1"] = [Number("pn-1", true)];
        await f.Complete(Session(CoexistenceEvent, phone: "pn-1"));
        var accountId = f.Db.Context.WhatsAppAccounts.Single().Id;

        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.RetryAsync("another-company", f.User.Id, accountId));
    }

    // ---- Meta error-code mapping (popup ERROR events and Graph errors) ------------------------

    [Theory]
    [InlineData("3441045", "number_not_eligible")]
    [InlineData("3441042", "region_not_supported")]
    [InlineData("4563015", "business_app_update_required")]
    [InlineData("2655095", "number_not_on_business_app")]
    [InlineData("2655048", "number_not_on_business_app")]
    [InlineData("2655011", "number_not_on_business_app")]
    [InlineData("3441030", "number_already_registered")]
    [InlineData("3441049", "number_already_registered")]
    [InlineData("2655094", "number_already_registered")]
    [InlineData("2655093", "already_shared_with_partner")]
    [InlineData("3441047", "marketing_messages_unsupported")]
    [InlineData("133016", "register_rate_limited")]
    [InlineData("190", "invalid_token")]
    [InlineData("999999", "provider_error")]
    [InlineData(null, "provider_error")]
    public void MetaCodes_MapToStableCodes(string? metaCode, string expected) =>
        Assert.Equal(expected, WhatsAppOnboardingErrors.FromMetaCode(metaCode));

    [Fact]
    public void ClientEvent_Error_MapsMetaCode_AndCancelIsCancelled()
    {
        using var f = new Fixture();

        var eligible = f.Service.ReportClientEvent(f.Company.Id, f.User.Id,
            new WhatsAppSignupClientEventRequest("ERROR", "PHONE_NUMBER_SETUP", "3441045", "ignored", "session-1"));
        var cancelled = f.Service.ReportClientEvent(f.Company.Id, f.User.Id,
            new WhatsAppSignupClientEventRequest("CANCEL", "PHONE_NUMBER_SETUP", null, null, null));
        var noCode = f.Service.ReportClientEvent(f.Company.Id, f.User.Id,
            new WhatsAppSignupClientEventRequest("ERROR", null, null, null, null));

        Assert.Equal("number_not_eligible", eligible.Code);
        Assert.Contains("More activity", eligible.Message);
        Assert.Equal("cancelled", cancelled.Code);
        Assert.Equal("unknown", noCode.Code);
    }

    [Fact]
    public void ClientEventValidator_OnlyAcceptsCancelAndError()
    {
        var validator = new WhatsAppSignupClientEventRequestValidator();
        Assert.True(validator.Validate(new WhatsAppSignupClientEventRequest("ERROR", null, null, null, null)).IsValid);
        Assert.True(validator.Validate(new WhatsAppSignupClientEventRequest("CANCEL", null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new WhatsAppSignupClientEventRequest("FINISH", null, null, null, null)).IsValid);
    }

    [Fact]
    public void EveryErrorCode_HasAMessage_AndAStatus()
    {
        var codes = typeof(WhatsAppOnboardingErrors).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.All(codes, code =>
        {
            Assert.False(string.IsNullOrWhiteSpace(WhatsAppOnboardingErrors.MessageFor(code)));
            Assert.InRange(WhatsAppOnboardingErrors.StatusFor(code), 400, 599);
        });
    }
}
