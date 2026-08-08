using FluentValidation;

namespace BusinessOS.Application.ViewModels.ApiKeys;

public record ApiKeyDto(
    string Id,
    string Name,
    string KeyPrefix,
    bool IsActive,
    string? CreatedByUserId,
    string? CreatedByUserName,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

// The only response shape that ever carries the raw key - returned once, at creation.
// Every other read (ListAsync, and this same key after creation) exposes KeyPrefix only.
public record CreateApiKeyResponse(string Id, string Name, string RawKey, string KeyPrefix, DateTime? ExpiresAt, DateTime CreatedAt);

public record CreateApiKeyRequest(string Name, DateTime? ExpiresAt = null);

public record UpdateApiKeyRequest(string? Name, bool? IsActive);

public class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateApiKeyRequestValidator : AbstractValidator<UpdateApiKeyRequest>
{
    public UpdateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
    }
}
