using System.Text.Json;
using BusinessOS.WebApi.Controllers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class WhatsAppDebugLogTests
{
    [Fact]
    public void Redact_HidesCodesTokensAndStateAtAnyDepth_ButKeepsDiagnostics()
    {
        using var doc = JsonDocument.Parse("""
            {"status":"connected","authResponse":{"code":"AQD-secret-code","keys":["code"]},"state":"signed.state","nested":{"access_token":"EAAB","event":"CANCEL"}}
            """);

        var text = WhatsAppDebugController.Redact(doc.RootElement);

        Assert.DoesNotContain("AQD-secret-code", text);
        Assert.DoesNotContain("signed.state", text);
        Assert.DoesNotContain("EAAB", text);
        Assert.Contains("\"status\":\"connected\"", text);
        Assert.Contains("\"event\":\"CANCEL\"", text);
    }

    [Fact]
    public void Redact_TruncatesLongPayloads()
    {
        using var doc = JsonDocument.Parse($"{{\"x\":\"{new string('a', 5000)}\"}}");
        Assert.True(WhatsAppDebugController.Redact(doc.RootElement).Length < 700);
    }
}
