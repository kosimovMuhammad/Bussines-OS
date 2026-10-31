using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Users;

// Dedicated presign/confirm pair for the user's own avatar/cover, distinct from
// Files' PresignUploadRequest/ConfirmFileRequest - those require a contact/deal/invoice
// relation that profile media doesn't have, and confirm here writes straight to the
// User row instead of creating a FileAsset.
public record PresignProfileMediaRequest(string Filename, string MimeType, ProfileMediaTarget Target);

public record ConfirmProfileMediaRequest(string S3Key, string Filename, string MimeType, int SizeBytes, ProfileMediaTarget Target);

public class PresignProfileMediaRequestValidator : AbstractValidator<PresignProfileMediaRequest>
{
    public PresignProfileMediaRequestValidator()
    {
        RuleFor(x => x.Filename).NotEmpty();
        RuleFor(x => x.MimeType).NotEmpty();
        RuleFor(x => x.Target).IsInEnum();
    }
}

public class ConfirmProfileMediaRequestValidator : AbstractValidator<ConfirmProfileMediaRequest>
{
    public ConfirmProfileMediaRequestValidator()
    {
        RuleFor(x => x.S3Key).NotEmpty();
        RuleFor(x => x.Filename).NotEmpty();
        RuleFor(x => x.MimeType).NotEmpty();
        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Target).IsInEnum();
    }
}
