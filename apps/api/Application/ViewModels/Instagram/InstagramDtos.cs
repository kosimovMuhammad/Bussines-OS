using System.Text.Json.Serialization;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Instagram;

// InstagramBusinessAccountId is intentionally not part of this request - it must come from
// GetAccountInfoAsync's response (the id the access token itself resolves to), never from the
// caller, since a user-supplied id can silently mismatch the token's real account.
public record ConnectInstagramAccountRequest(string AccessToken);

public record InstagramAccountDto(
    string Id,
    string CompanyId,
    string InstagramBusinessAccountId,
    string Username,
    bool IsActive,
    DateTime? TokenExpiresAt,
    DateTime CreatedAt);

public record SendInstagramMessageRequest(string ContactId, string Message);

public record InstagramConversationLinkDto(
    string Id,
    string CompanyId,
    string? ContactId,
    string InstagramScopedId,
    string? InstagramUsername,
    InstagramLinkMethod LinkMethod,
    DateTime? LinkedAt,
    string? LastMessagePreview,
    DateTime CreatedAt);

public record LinkInstagramConversationToContactRequest(string ContactId);

// Response shape of Graph API's GET /{ig-user-id}?fields=id,user_id,username,name.
// Id is the app-scoped id (NOT what webhooks use); UserId is the Instagram professional
// account id (<IG_ID>) that matches recipient.id/sender.id in webhook notifications - see
// InstagramApiClient for the full explanation.
public record InstagramUserInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name);

// ExpiresAt is null for the short-lived token returned by ExchangeCodeAsync (Meta doesn't
// give it a meaningful expiry worth storing - it's immediately exchanged for a long-lived
// token) and set for ExchangeForLongLivedTokenAsync/RefreshLongLivedTokenAsync (~60 days out).
public record InstagramOAuthTokenResult(string AccessToken, DateTime? ExpiresAt);

public class ConnectInstagramAccountRequestValidator : AbstractValidator<ConnectInstagramAccountRequest>
{
    public ConnectInstagramAccountRequestValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

public class SendInstagramMessageRequestValidator : AbstractValidator<SendInstagramMessageRequest>
{
    public SendInstagramMessageRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public class LinkInstagramConversationToContactRequestValidator : AbstractValidator<LinkInstagramConversationToContactRequest>
{
    public LinkInstagramConversationToContactRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
    }
}
