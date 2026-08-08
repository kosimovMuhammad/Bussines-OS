using FluentValidation;

namespace BusinessOS.Application.ViewModels.Files;

public record PresignUploadRequest(string Filename, string MimeType, string? ContactId, string? DealId, string? InvoiceId);

public record PresignUploadResponse(string UploadUrl, string S3Key);

public record ConfirmFileRequest(string S3Key, string Filename, string MimeType, int SizeBytes, string? ContactId, string? DealId, string? InvoiceId);

public record QueryFilesRequest(string? ContactId, string? DealId, string? InvoiceId);

public record FileAssetDto(
    string Id,
    string CompanyId,
    string? ContactId,
    string? DealId,
    string? InvoiceId,
    string UploadedById,
    string UploadedByName,
    string Filename,
    string MimeType,
    int SizeBytes,
    string? DownloadUrl,
    DateTime CreatedAt);

public class PresignUploadRequestValidator : AbstractValidator<PresignUploadRequest>
{
    public PresignUploadRequestValidator()
    {
        RuleFor(x => x.Filename).NotEmpty();
        RuleFor(x => x.MimeType).NotEmpty();
        RuleFor(x => x).Must(x => x.ContactId is not null || x.DealId is not null || x.InvoiceId is not null)
            .WithMessage("At least one of contactId/dealId/invoiceId is required");
    }
}

public class ConfirmFileRequestValidator : AbstractValidator<ConfirmFileRequest>
{
    public ConfirmFileRequestValidator()
    {
        RuleFor(x => x.S3Key).NotEmpty();
        RuleFor(x => x.Filename).NotEmpty();
        RuleFor(x => x.MimeType).NotEmpty();
        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(1);
        RuleFor(x => x).Must(x => x.ContactId is not null || x.DealId is not null || x.InvoiceId is not null)
            .WithMessage("At least one of contactId/dealId/invoiceId is required");
    }
}

public record FileVersionDto(
    string Id,
    int VersionNumber,
    string Filename,
    string MimeType,
    int SizeBytes,
    string UploadedById,
    string UploadedByName,
    DateTime CreatedAt);

public record RestoreFileVersionRequest(string VersionId);

public class RestoreFileVersionRequestValidator : AbstractValidator<RestoreFileVersionRequest>
{
    public RestoreFileVersionRequestValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
    }
}
