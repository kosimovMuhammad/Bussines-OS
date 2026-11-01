using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Development-only sink for the frontend's [WA-ES] step trace, so browser-side Embedded Signup
// steps land in the same log file as the backend's. 404 outside Development.
[ApiController]
[Route("whatsapp/embedded-signup/debug")]
[Authorize]
public class WhatsAppDebugController(IWebHostEnvironment environment, ILogger<WhatsAppDebugController> logger) : ControllerBase
{
    private static readonly string[] SensitiveKeys = ["code", "access_token", "accesstoken", "token", "secret", "state", "password"];
    private const int MaxLength = 600;

    public record DebugStep(string Step, JsonElement? Data);

    [HttpPost]
    public IActionResult Post(DebugStep body)
    {
        if (!environment.IsDevelopment()) return NotFound();

        var data = body.Data is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } d ? Redact(d) : "-";
        logger.LogInformation("[WA-ES] {Step} {Data}", Truncate(body.Step, 100), data);
        return NoContent();
    }

    // Never logs one-time codes, tokens or the signed state, even if the frontend sends them.
    public static string Redact(JsonElement element)
    {
        var sb = new System.Text.StringBuilder();
        Write(element, sb, null);
        return Truncate(sb.ToString(), MaxLength);
    }

    private static void Write(JsonElement e, System.Text.StringBuilder sb, string? key)
    {
        if (key is not null && SensitiveKeys.Contains(key.ToLowerInvariant()))
        {
            sb.Append(e.ValueKind is JsonValueKind.Null ? "null" : "\"<redacted>\"");
            return;
        }

        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var first = true;
                foreach (var p in e.EnumerateObject())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(p.Name).Append("\":");
                    Write(p.Value, sb, p.Name);
                }
                sb.Append('}');
                break;
            case JsonValueKind.Array:
                sb.Append('[');
                var firstItem = true;
                foreach (var item in e.EnumerateArray())
                {
                    if (!firstItem) sb.Append(',');
                    firstItem = false;
                    Write(item, sb, null);
                }
                sb.Append(']');
                break;
            default:
                sb.Append(e.GetRawText());
                break;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
