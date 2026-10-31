using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Abstraction;

public record RefreshTokenPayload(string UserId, string Jti);

public interface ITokenService
{
    string CreateAccessToken(string userId, string companyId, Role role, string? branchId = null);

    // Returns the signed JWT and its jti claim (persisted, hashed, as the RefreshToken row id).
    (string Token, string Jti) CreateRefreshToken(string userId);

    RefreshTokenPayload? ValidateRefreshToken(string token);

    // Decodes without checking signature/expiry - logout must still clear the DB row
    // for an already-expired or already-invalid refresh token, matching the original.
    RefreshTokenPayload? DecodeRefreshTokenUnsafe(string token);
}
