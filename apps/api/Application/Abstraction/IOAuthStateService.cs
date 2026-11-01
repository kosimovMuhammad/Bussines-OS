namespace BusinessOS.Application.Abstraction;

// Locale is optional (null for states minted before it existed) so the public callback can
// redirect the user back to /{locale}/... in the language they started in.
public record OAuthStatePayload(string UserId, string CompanyId, string? Locale = null);

// Signs a short-lived {userId, companyId, locale} tuple into the OAuth "state" param so public
// OAuth callback endpoints (no bearer token available) can identify who's connecting.
public interface IOAuthStateService
{
    string Create(string userId, string companyId, string? locale = null);
    OAuthStatePayload? Validate(string state);
}
