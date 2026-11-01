using System.Net;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Configurations;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.WhatsApp;
using BusinessOS.Tests.TestSupport;
using BusinessOS.WebApi.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusinessOS.Tests.Unit;

// WhatsApp Phase 1 (foundation): configuration validation, the single Graph API version, the new
// Graph onboarding calls, hard-delete disconnect, and the camelCase JSON contract.
public class WhatsAppConfigurationValidationTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static IConfiguration Config(params (string, string)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Item1, p => (string?)p.Item2)).Build();

    [Fact]
    public void AppSecretAndVerifyTokenWithoutAppId_Fails()
    {
        var errors = WebhookConfigurationValidator.Validate(Config(
            ("WHATSAPP_APP_SECRET", Secret), ("WHATSAPP_VERIFY_TOKEN", Secret)));
        Assert.Contains(errors, e => e.StartsWith("WHATSAPP_APP_ID"));
    }

    [Fact]
    public void EmbeddedSignupConfigIdAlone_RequiresTheRest()
    {
        var errors = WebhookConfigurationValidator.Validate(Config(("WHATSAPP_ES_CONFIG_ID", "123456")));
        Assert.Contains(errors, e => e.StartsWith("WHATSAPP_APP_ID"));
        Assert.Contains(errors, e => e.StartsWith("WHATSAPP_APP_SECRET"));
        Assert.Contains(errors, e => e.StartsWith("WHATSAPP_VERIFY_TOKEN"));
    }

    [Fact]
    public void HttpPublicUrl_Fails() =>
        Assert.Contains(WebhookConfigurationValidator.Validate(Config(
            ("WHATSAPP_APP_ID", "1"), ("WHATSAPP_APP_SECRET", Secret), ("WHATSAPP_VERIFY_TOKEN", Secret),
            ("PUBLIC_URL", "http://example.com"))), e => e.Contains("PUBLIC_URL") && e.Contains("WhatsApp"));

    [Theory]
    [InlineData("26.0")]
    [InlineData("latest")]
    [InlineData("v26")]
    [InlineData("v26.0.1")]
    public void MalformedApiVersion_Fails(string version) =>
        Assert.Contains(WebhookConfigurationValidator.Validate(Config(
            ("WHATSAPP_APP_ID", "1"), ("WHATSAPP_APP_SECRET", Secret), ("WHATSAPP_VERIFY_TOKEN", Secret),
            ("WHATSAPP_API_VERSION", version))), e => e.StartsWith("WHATSAPP_API_VERSION"));

    [Fact]
    public void FullyConfigured_Passes() =>
        Assert.Empty(WebhookConfigurationValidator.Validate(Config(
            ("WHATSAPP_APP_ID", "1"), ("WHATSAPP_APP_SECRET", Secret), ("WHATSAPP_VERIFY_TOKEN", Secret),
            ("WHATSAPP_ES_CONFIG_ID", "123456"), ("WHATSAPP_API_VERSION", "v26.0"), ("PUBLIC_URL", "https://x.ngrok-free.dev"))));
}

public class WhatsAppJsonContractTests
{
    // ASP.NET Core MVC binds with JsonSerializerDefaults.Web (camelCase, case-insensitive) and the
    // app adds only a string-enum converter (Program.cs) - there is NO snake_case policy, so a
    // phone_number_id body does not bind. Pinned here so the frontend contract can't drift silently.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CompleteRequest_BindsCamelCase_IncludingNestedSession()
    {
        var request = JsonSerializer.Deserialize<CompleteWhatsAppEmbeddedSignupRequest>(
            """
            {"code":"c","state":"s","session":{"event":"FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING","phoneNumberId":"p",
             "messagingAccountIds":["w1"],"businessId":"b","isOnBizApp":true}}
            """, Web)!;

        Assert.Equal(("c", "s"), (request.Code, request.State));
        Assert.Equal("FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING", request.Session!.Event);
        Assert.Equal("p", request.Session.PhoneNumberId);
        Assert.Equal(["w1"], request.Session.MessagingAccountIds);
        Assert.True(request.Session.IsOnBizApp);
    }

    [Fact]
    public void CompleteRequest_SnakeCaseSessionDoesNotBind()
    {
        var request = JsonSerializer.Deserialize<CompleteWhatsAppEmbeddedSignupRequest>(
            """{"code":"c","state":"s","session":{"phone_number_id":"p","messaging_account_ids":["w1"],"is_on_biz_app":true}}""", Web)!;

        Assert.Null(request.Session!.PhoneNumberId);
        Assert.Null(request.Session.MessagingAccountIds);
        Assert.Null(request.Session.IsOnBizApp);
    }

    [Fact]
    public void CompleteRequest_MissingCodeOrState_FailsValidation()
    {
        var validator = new CompleteWhatsAppEmbeddedSignupRequestValidator();
        Assert.False(validator.Validate(new CompleteWhatsAppEmbeddedSignupRequest("", "s", null)).IsValid);
        Assert.False(validator.Validate(new CompleteWhatsAppEmbeddedSignupRequest("c", "", null)).IsValid);
        Assert.True(validator.Validate(new CompleteWhatsAppEmbeddedSignupRequest("c", "s", null)).IsValid);
    }

    [Fact]
    public void AccountDto_SerializesCamelCaseWithStringEnums()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var dto = new WhatsAppAccountDto("id", "co", "pn", "waba", "Name", "+992900000001", true, true,
            WhatsAppConnectionStatus.NeedsAttention, WhatsAppHistorySyncStatus.Pending, null, null, DateTime.UtcNow);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, options));

        Assert.Equal("pn", doc.RootElement.GetProperty("phoneNumberId").GetString());
        Assert.Equal("+992900000001", doc.RootElement.GetProperty("displayPhoneNumber").GetString());
        Assert.True(doc.RootElement.GetProperty("isOnBizApp").GetBoolean());
        Assert.Equal("NeedsAttention", doc.RootElement.GetProperty("connectionStatus").GetString());
        Assert.Equal("Pending", doc.RootElement.GetProperty("historySyncStatus").GetString());
    }
}

public class MetaWhatsAppClientGraphTests
{
    private sealed record Captured(HttpMethod Method, Uri Uri, string? AuthScheme, string? AuthParameter, string Body);

    private sealed class StubHandler(HttpStatusCode status, string responseJson) : HttpMessageHandler
    {
        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Captured(request.Method, request.RequestUri!, request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter, body));
            return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (MetaWhatsAppClient Client, StubHandler Handler) Build(
        HttpStatusCode status, string responseJson, string? apiVersion = null)
    {
        var handler = new StubHandler(status, responseJson);
        var options = new WhatsAppPlatformOptions();
        if (apiVersion is not null)
        {
            options.ApiVersion = apiVersion;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WHATSAPP_APP_ID"] = "app-id",
            ["WHATSAPP_APP_SECRET"] = "app-secret",
        }).Build();

        var client = new MetaWhatsAppClient(new StubFactory(handler), Options.Create(options), configuration, NullLogger<MetaWhatsAppClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public void DefaultApiVersion_IsCurrentAndNotTheOldHardcodedOne()
    {
        Assert.Equal("v26.0", WhatsAppPlatformOptions.DefaultApiVersion);
        Assert.Equal("v26.0", new WhatsAppPlatformOptions().ApiVersion);
    }

    [Fact]
    public async Task TenantSend_UsesConfiguredApiVersion()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"messages":[{"id":"wamid.1"}]}""", "v99.1");

        var id = await client.SendTextMessageAsync("pn-1", "tok", "992900000001", "hi");

        Assert.Equal("wamid.1", id);
        Assert.StartsWith("https://graph.facebook.com/v99.1/pn-1/messages", Assert.Single(handler.Requests).Uri.ToString());
    }

    [Fact]
    public async Task OAuthExchange_UsesConfiguredApiVersion()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"access_token":"t"}""", "v99.1");

        await client.ExchangeCodeAsync("code-1");

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("https://graph.facebook.com/v99.1/oauth/access_token?", request.Uri.ToString());
        Assert.DoesNotContain("v20.0", request.Uri.ToString());
    }

    private sealed class SequenceHandler(params (HttpStatusCode Status, string Json)[] responses) : HttpMessageHandler
    {
        public List<Uri> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, json) = responses[Math.Min(Uris.Count, responses.Length - 1)];
            Uris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static MetaWhatsAppClient BuildSequence(SequenceHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WHATSAPP_APP_ID"] = "app-id",
            ["WHATSAPP_APP_SECRET"] = "app-secret",
        }).Build();
        return new MetaWhatsAppClient(new StubFactory(handler), Options.Create(new WhatsAppPlatformOptions()), configuration, NullLogger<MetaWhatsAppClient>.Instance);
    }

    private const string RedirectUriError = """{"error":{"message":"Error validating verification code. Please make sure your redirect_uri is identical to the one you used in the OAuth dialog request","code":100,"fbtrace_id":"abc"}}""";

    [Fact]
    public async Task ExchangeCode_FirstAttemptOmitsRedirectUri()
    {
        var handler = new SequenceHandler((HttpStatusCode.OK, """{"access_token":"t"}"""));

        await BuildSequence(handler).ExchangeCodeAsync("code-1");

        var uri = Assert.Single(handler.Uris);
        Assert.DoesNotContain("redirect_uri", uri.Query);
    }

    [Fact]
    public async Task ExchangeCode_RedirectUriError_IsNotRetried()
    {
        // Live test: no redirect_uri variant rescues a rejected code, so exactly one call is made.
        var handler = new SequenceHandler((HttpStatusCode.BadRequest, RedirectUriError));

        await Assert.ThrowsAsync<WhatsAppApiException>(() => BuildSequence(handler).ExchangeCodeAsync("code-1"));

        Assert.Single(handler.Uris);
    }

    [Fact]
    public async Task ExchangeCode_OtherError_DoesNotRetry()
    {
        var handler = new SequenceHandler((HttpStatusCode.BadRequest,
            """{"error":{"message":"Invalid client secret","code":101,"fbtrace_id":"x"}}"""));

        await Assert.ThrowsAsync<WhatsAppApiException>(() => BuildSequence(handler).ExchangeCodeAsync("code-1"));

        Assert.Single(handler.Uris);
    }

    [Fact]
    public async Task DebugToken_ParsesGranularScopes_AndTreatsZeroExpiryAsNever()
    {
        const string json = """
            {"data":{"app_id":"app-id","is_valid":true,"user_id":"fb-user-1","expires_at":0,
             "granular_scopes":[
               {"scope":"whatsapp_business_management","target_ids":["waba-1","waba-2"]},
               {"scope":"whatsapp_business_messaging","target_ids":["waba-1"]},
               {"scope":"business_management"}]}}
            """;
        var (client, handler) = Build(HttpStatusCode.OK, json);

        var info = await client.DebugTokenAsync("the token/with+chars");

        Assert.True(info.IsValid);
        Assert.Equal("fb-user-1", info.UserId);
        Assert.Null(info.ExpiresAt);
        Assert.Equal(["waba-1", "waba-2"], info.TargetIdsFor("whatsapp_business_management"));
        Assert.Empty(info.TargetIdsFor("business_management"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("/debug_token?input_token=the%20token%2Fwith%2Bchars", request.Uri.AbsoluteUri);
        // App access token travels in the header, never in the URL.
        Assert.Equal("app-id|app-secret", request.AuthParameter);
        Assert.DoesNotContain("app-secret", request.Uri.ToString());
    }

    [Fact]
    public async Task DebugToken_RealExpiry_IsConvertedToUtc()
    {
        var (client, _) = Build(HttpStatusCode.OK, """{"data":{"is_valid":true,"expires_at":1893456000,"granular_scopes":[]}}""");
        var info = await client.DebugTokenAsync("t");
        Assert.Equal(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), info.ExpiresAt);
    }

    [Fact]
    public async Task DebugToken_InvalidToken_ReportsNotValid()
    {
        var (client, _) = Build(HttpStatusCode.OK, """{"data":{"is_valid":false,"error":{"code":190,"message":"expired"}}}""");
        Assert.False((await client.DebugTokenAsync("t")).IsValid);
    }

    [Fact]
    public async Task GetWabaPhoneNumbers_ParsesCoexistenceFlags()
    {
        const string json = """
            {"data":[
              {"id":"pn-1","display_phone_number":"+992 90 000 0001","verified_name":"Shop","platform_type":"CLOUD_API","is_on_biz_app":true,"status":"CONNECTED"},
              {"id":"pn-2","display_phone_number":"+992 90 000 0002"}]}
            """;
        var (client, handler) = Build(HttpStatusCode.OK, json);

        var numbers = await client.GetWabaPhoneNumbersAsync("tok", "waba-1");

        Assert.Equal(2, numbers.Count);
        Assert.Equal(("pn-1", true, "CLOUD_API", "CONNECTED"), (numbers[0].Id, numbers[0].IsOnBizApp, numbers[0].PlatformType, numbers[0].Status));
        Assert.Null(numbers[1].IsOnBizApp);
        Assert.Contains("/waba-1/phone_numbers?fields=", Assert.Single(handler.Requests).Uri.ToString());
    }

    [Fact]
    public async Task GetPhoneNumber_RequestsOnBizAppFields()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"id":"pn-1","is_on_biz_app":false,"platform_type":"CLOUD_API"}""");

        var info = await client.GetPhoneNumberAsync("tok", "pn-1");

        Assert.False(info.IsOnBizApp);
        var uri = Assert.Single(handler.Requests).Uri.ToString();
        Assert.Contains("is_on_biz_app", uri);
        Assert.Contains("platform_type", uri);
    }

    [Fact]
    public async Task Register_PostsMessagingProductAndPin()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"success":true}""");

        await client.RegisterPhoneNumberAsync("tok", "pn-1", "123456");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/pn-1/register", request.Uri.AbsolutePath);
        Assert.Equal("Bearer", request.AuthScheme);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("whatsapp", body.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal("123456", body.RootElement.GetProperty("pin").GetString());
    }

    [Fact]
    public async Task SmbAppDataSync_PostsSyncTypeAndReturnsRequestId()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"messaging_product":"whatsapp","request_id":"req-1"}""");

        var requestId = await client.RequestSmbAppDataSyncAsync("tok", "pn-1", WhatsAppSyncTypes.History);

        Assert.Equal("req-1", requestId);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/pn-1/smb_app_data", request.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("history", body.RootElement.GetProperty("sync_type").GetString());
    }

    [Fact]
    public async Task Subscribe_Posts_Unsubscribe_Deletes_SubscribedApps()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"success":true}""");

        await client.SubscribeWebhookAsync("tok", "waba-1");
        await client.UnsubscribeWebhookAsync("tok", "waba-1");

        Assert.Equal([HttpMethod.Post, HttpMethod.Delete], handler.Requests.Select(r => r.Method));
        Assert.All(handler.Requests, r => Assert.EndsWith("/waba-1/subscribed_apps", r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task GraphError_ThrowsWhatsAppApiExceptionWithMetaCode()
    {
        var (client, _) = Build(HttpStatusCode.BadRequest,
            """{"error":{"message":"Too many register attempts","code":133016,"fbtrace_id":"trace-1"}}""");

        var ex = await Assert.ThrowsAsync<WhatsAppApiException>(() => client.RegisterPhoneNumberAsync("tok", "pn-1", "123456"));

        Assert.Equal("133016", ex.MetaErrorCode);
        Assert.Equal("trace-1", ex.FbTraceId);
        Assert.Equal(400, ex.ProviderStatusCode);
        Assert.Contains("Too many register attempts", ex.Message);
    }
}

public class WhatsAppAccountLifecycleTests
{
    private static (WhatsAppService Service, FakeWhatsAppClient Client) Build(TestDb db)
    {
        var client = new FakeWhatsAppClient();
        var service = new WhatsAppService(
            db.Context, client, new FakeTokenCipher(), new FakeRealtimeNotifier(), new AuditLogService(db.Context),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<WhatsAppService>.Instance);
        return (service, client);
    }

    // The audit log has a foreign key to users, so the actor must be a real row.
    private static string ActorId(TestDb db, string companyId)
    {
        var user = new User { CompanyId = companyId, Name = "Actor", Email = $"{Guid.NewGuid():N}@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user.Id;
    }

    private static Company AddCompany(TestDb db, string name = "Acme")
    {
        var company = new Company { Name = name };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }

    private static WhatsAppAccount AddAccount(TestDb db, string companyId, string phoneNumberId = "pn-1", string wabaId = "waba-1")
    {
        var account = new WhatsAppAccount
        {
            CompanyId = companyId,
            PhoneNumberId = phoneNumberId,
            WabaId = wabaId,
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            DisplayPhoneNumber = "+992 90 000 0001",
            IsOnBizApp = true,
        };
        db.Context.WhatsAppAccounts.Add(account);
        db.Context.SaveChanges();
        return account;
    }

    [Fact]
    public async Task Remove_HardDeletesRow_AndUnsubscribesWaba()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = AddCompany(db);
        var account = AddAccount(db, company.Id);

        await service.RemoveAccountAsync(company.Id, ActorId(db, company.Id), account.Id);

        Assert.Empty(db.Context.WhatsAppAccounts);
        Assert.Equal(["waba-1"], client.UnsubscribedWabaIds);
    }

    [Fact]
    public async Task Remove_KeepsMessageHistory()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = AddCompany(db);
        var account = AddAccount(db, company.Id);
        var contact = new Contact { CompanyId = company.Id, FirstName = "A", Phone = "+992900000001" };
        db.Context.Contacts.Add(contact);
        db.Context.Communications.Add(new Communication
        {
            CompanyId = company.Id, ContactId = contact.Id, Channel = CommunicationChannel.Whatsapp,
            Direction = CommunicationDirection.Inbound, Content = "hi", ProviderMessageId = "wamid.1",
        });
        db.Context.SaveChanges();

        await service.RemoveAccountAsync(company.Id, ActorId(db, company.Id), account.Id);

        Assert.Single(db.Context.Communications);
        Assert.Single(db.Context.Contacts);
    }

    [Fact]
    public async Task Remove_FreesThePhoneNumberForAnotherCompany()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var first = AddCompany(db, "First");
        var second = AddCompany(db, "Second");
        var account = AddAccount(db, first.Id);

        await service.RemoveAccountAsync(first.Id, ActorId(db, first.Id), account.Id);

        // The globally unique PhoneNumberId is only claimable again because the row is really gone
        // (a soft delete would have kept it claimed forever).
        AddAccount(db, second.Id);
        Assert.Equal(second.Id, Assert.Single(db.Context.WhatsAppAccounts).CompanyId);
    }

    [Fact]
    public async Task Remove_DoesNotUnsubscribeWaba_WhileASiblingNumberStillUsesIt()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = AddCompany(db);
        var first = AddAccount(db, company.Id, "pn-1", "waba-shared");
        AddAccount(db, company.Id, "pn-2", "waba-shared");

        await service.RemoveAccountAsync(company.Id, ActorId(db, company.Id), first.Id);

        Assert.Empty(client.UnsubscribedWabaIds);
        Assert.Equal("pn-2", Assert.Single(db.Context.WhatsAppAccounts).PhoneNumberId);
    }

    [Fact]
    public async Task Remove_UnsubscribeFailure_DoesNotBlockDisconnect()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = AddCompany(db);
        var account = AddAccount(db, company.Id);
        client.NextUnsubscribeException = new WhatsAppApiException("token revoked", "190");

        await service.RemoveAccountAsync(company.Id, ActorId(db, company.Id), account.Id);

        Assert.Empty(db.Context.WhatsAppAccounts);
    }

    [Fact]
    public async Task Remove_OtherCompanysAccount_ThrowsNotFound_AndChangesNothing()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var owner = AddCompany(db, "Owner");
        var other = AddCompany(db, "Other");
        var account = AddAccount(db, owner.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveAccountAsync(other.Id, ActorId(db, owner.Id), account.Id));

        Assert.Single(db.Context.WhatsAppAccounts);
        Assert.Empty(client.UnsubscribedWabaIds);
    }

    [Fact]
    public async Task List_ExposesOnboardingFields_AndNeverTheToken()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = AddCompany(db);
        AddAccount(db, company.Id);

        var dto = Assert.Single(await service.ListAccountsAsync(company.Id));

        Assert.Equal("+992 90 000 0001", dto.DisplayPhoneNumber);
        Assert.True(dto.IsOnBizApp);
        Assert.Equal(WhatsAppConnectionStatus.Active, dto.ConnectionStatus);
        Assert.Equal(WhatsAppHistorySyncStatus.NotApplicable, dto.HistorySyncStatus);
        Assert.DoesNotContain(typeof(WhatsAppAccountDto).GetProperties(), p => p.Name.Contains("Token") && p.Name != "TokenExpiresAt");
        Assert.DoesNotContain(typeof(WhatsAppAccountDto).GetProperties(), p => p.Name.Contains("Pin"));
    }
}
