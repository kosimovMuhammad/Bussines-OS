namespace BusinessOS.Application.Abstraction;

public record OAuthStatePayload(string UserId, string CompanyId);

// Signs a short-lived {userId, companyId} pair into the OAuth "state" param so public
// OAuth callback endpoints (no bearer token available) can identify who's connecting.
public interface IOAuthStateService
{
    string Create(string userId, string companyId);
    OAuthStatePayload? Validate(string state);
}
