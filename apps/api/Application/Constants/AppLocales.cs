namespace BusinessOS.Application.Constants;

// Locales the frontend serves under /{locale}/... Anything else falls back to the default so a
// tampered or stale value can never produce an open-redirect-style path.
public static class AppLocales
{
    public const string Default = "en";

    private static readonly string[] Supported = ["en", "ru", "tj"];

    public static string Normalize(string? locale)
    {
        var candidate = locale?.Trim().ToLowerInvariant();
        return candidate is not null && Supported.Contains(candidate) ? candidate : Default;
    }
}
