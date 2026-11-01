using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Webhooks;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Tests.Unit;

public class MetaSignedRequestTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void TryGetUserId_ValidSignature_ReturnsUserId()
    {
        var signed = MetaSignedRequest.Create("1784", Secret);
        Assert.True(MetaSignedRequest.TryGetUserId(signed, Secret, out var userId));
        Assert.Equal("1784", userId);
    }

    [Fact]
    public void TryGetUserId_WrongSecret_Rejected() =>
        Assert.False(MetaSignedRequest.TryGetUserId(MetaSignedRequest.Create("1784", Secret), "another-secret-value-123456", out _));

    [Fact]
    public void TryGetUserId_TamperedPayload_Rejected()
    {
        var parts = MetaSignedRequest.Create("1784", Secret).Split('.');
        var forged = MetaSignedRequest.Create("9999", Secret).Split('.')[1];
        Assert.False(MetaSignedRequest.TryGetUserId($"{parts[0]}.{forged}", Secret, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nodot")]
    [InlineData("a.b")]
    [InlineData("!!!.???")]
    public void TryGetUserId_Garbage_Rejected(string? input) =>
        Assert.False(MetaSignedRequest.TryGetUserId(input, Secret, out _));

    [Fact]
    public void TryGetUserId_NoSecretConfigured_Rejected() =>
        Assert.False(MetaSignedRequest.TryGetUserId(MetaSignedRequest.Create("1", Secret), null, out _));

    [Fact]
    public void ConfirmationCode_RoundTripsAndRejectsForgery()
    {
        var code = MetaSignedRequest.CreateConfirmationCode(Secret);
        Assert.True(MetaSignedRequest.IsValidConfirmationCode(code, Secret));
        Assert.False(MetaSignedRequest.IsValidConfirmationCode(code, "another-secret-value-123456"));
        Assert.False(MetaSignedRequest.IsValidConfirmationCode("0000000000000000.deadbeef", Secret));
        Assert.False(MetaSignedRequest.IsValidConfirmationCode(null, Secret));
    }
}

public class AppLocalesTests
{
    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("TJ", "tj")]
    [InlineData(" en ", "en")]
    [InlineData("de", "en")]
    [InlineData("../evil", "en")]
    [InlineData(null, "en")]
    public void Normalize(string? input, string expected) => Assert.Equal(expected, AppLocales.Normalize(input));
}

public class WebhookSecurityTests
{
    [Fact]
    public void SecretsEqual_ComparesWithoutLeakingLength()
    {
        Assert.True(WebhookSecurity.SecretsEqual("abc", "abc"));
        Assert.False(WebhookSecurity.SecretsEqual("abc", "abcd"));
        Assert.False(WebhookSecurity.SecretsEqual("", ""));
        Assert.False(WebhookSecurity.SecretsEqual(null, "x"));
    }

    [Fact]
    public void IsValidMetaSignature_OverRawBytes()
    {
        var body = System.Text.Encoding.UTF8.GetBytes("{\"a\":1}");
        var hex = Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("secret"), body));
        Assert.True(WebhookSecurity.IsValidMetaSignature(body, "sha256=" + hex, "secret"));
        Assert.True(WebhookSecurity.IsValidMetaSignature(body, "sha256=" + hex.ToUpperInvariant(), "secret"));
        Assert.False(WebhookSecurity.IsValidMetaSignature(body, "sha256=" + hex, "other"));
        Assert.False(WebhookSecurity.IsValidMetaSignature(body, "sha256=zz", "secret"));
        Assert.False(WebhookSecurity.IsValidMetaSignature(body, null, "secret"));
        Assert.False(WebhookSecurity.IsValidMetaSignature(body, "sha256=" + hex, null));
    }

    [Fact]
    public void Verification_RequiresSubscribeModeAndToken()
    {
        Assert.True(WebhookSecurity.IsValidVerification("subscribe", "tok", "tok"));
        Assert.False(WebhookSecurity.IsValidVerification("unsubscribe", "tok", "tok"));
        Assert.False(WebhookSecurity.IsValidVerification("subscribe", "bad", "tok"));
        Assert.False(WebhookSecurity.IsValidVerification("subscribe", "tok", null));
    }
}

public class WebhookConfigurationValidatorTests
{
    private static IConfiguration Config(params (string, string)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Item1, p => (string?)p.Item2)).Build();

    [Fact]
    public void NothingConfigured_NoErrors() => Assert.Empty(WebhookConfigurationValidator.Validate(Config()));

    [Fact]
    public void Instagram_PartiallyConfigured_Fails()
    {
        var errors = WebhookConfigurationValidator.Validate(Config(("INSTAGRAM_APP_ID", "123")));
        Assert.Contains(errors, e => e.StartsWith("INSTAGRAM_APP_SECRET"));
        Assert.Contains(errors, e => e.StartsWith("INSTAGRAM_VERIFY_TOKEN"));
    }

    [Fact]
    public void Instagram_ShortVerifyToken_And_HttpPublicUrl_Fail()
    {
        var errors = WebhookConfigurationValidator.Validate(Config(
            ("INSTAGRAM_APP_ID", "123"), ("INSTAGRAM_APP_SECRET", "0123456789abcdef0123456789abcdef"),
            ("INSTAGRAM_VERIFY_TOKEN", "short"), ("PUBLIC_URL", "http://example.com")));
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Instagram_FullyConfigured_Passes() =>
        Assert.Empty(WebhookConfigurationValidator.Validate(Config(
            ("INSTAGRAM_APP_ID", "123"), ("INSTAGRAM_APP_SECRET", "0123456789abcdef0123456789abcdef"),
            ("INSTAGRAM_VERIFY_TOKEN", "a-long-random-verify-token"), ("PUBLIC_URL", "https://x.ngrok-free.dev"))));

    [Fact]
    public void WhatsApp_SecretWithoutVerifyToken_Fails() =>
        Assert.Contains(WebhookConfigurationValidator.Validate(Config(("WHATSAPP_APP_SECRET", "0123456789abcdef0123456789abcdef"))),
            e => e.StartsWith("WHATSAPP_VERIFY_TOKEN"));
}
