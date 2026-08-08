using System.Text.RegularExpressions;

namespace BusinessOS.Application.Services;

// Minimal, deliberately unsafe-free template resolver for action config strings
// ("{{contact.name}}", "{{deal.value}}") - plain token substitution only, no expressions,
// no loops, no arbitrary code evaluation. An unresolvable token is left as-is rather than
// throwing or silently blanking, so a config typo is visible in the output instead of
// disappearing.
public static partial class TemplateResolver
{
    [GeneratedRegex(@"\{\{\s*([\w.]+)\s*\}\}")]
    private static partial Regex TokenPattern();

    public static string Resolve(string template, IReadOnlyDictionary<string, string?> values) =>
        TokenPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value ?? string.Empty : match.Value;
        });
}
