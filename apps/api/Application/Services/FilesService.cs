using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Files;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class FilesService(IAppDbContext db, IFileStorageService storage) : IFilesService
{
    public async Task<PresignUploadResponse> PresignAsync(string companyId, PresignUploadRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateRelationsAsync(request.ContactId, request.DealId, request.InvoiceId, cancellationToken);

        var key = storage.BuildKey(companyId, request.Filename);
        string url;
        try
        {
            url = storage.GetPresignedUploadUrl(key, request.MimeType);
        }
        catch (InvalidOperationException ex)
        {
            throw new ExternalServiceException($"File storage is not available: {ex.Message}");
        }

        return new PresignUploadResponse(url, key);
    }

    public async Task<FileAssetDto> ConfirmAsync(string companyId, string uploadedById, ConfirmFileRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateRelationsAsync(request.ContactId, request.DealId, request.InvoiceId, cancellationToken);

        var file = new FileAsset
        {
            CompanyId = companyId,
            ContactId = request.ContactId,
            DealId = request.DealId,
            InvoiceId = request.InvoiceId,
            UploadedById = uploadedById,
            S3Key = request.S3Key,
            Filename = request.Filename,
            MimeType = request.MimeType,
            SizeBytes = request.SizeBytes,
        };

        db.FileAssets.Add(file);

        // Seeds version 1 at upload time - there's no "upload a new version of an existing
        // file" endpoint yet (out of scope for this phase; would need a small addition to
        // ConfirmFileRequest to accept an existing fileId), so this is the only place a
        // FileVersion row is created today. GET .../versions and .../restore-version are
        // still meaningful without that: every file has at least this one snapshot.
        db.FileVersions.Add(new FileVersion
        {
            CompanyId = companyId,
            FileAsset = file,
            VersionNumber = 1,
            S3Key = file.S3Key,
            Filename = file.Filename,
            MimeType = file.MimeType,
            SizeBytes = file.SizeBytes,
            UploadedById = uploadedById,
        });

        await db.SaveChangesAsync(cancellationToken);

        file.UploadedBy = await db.Users.SingleAsync(u => u.Id == uploadedById, cancellationToken);
        return ToDto(file);
    }

    public async Task<IReadOnlyList<FileAssetDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryFilesRequest query, CancellationToken cancellationToken = default)
    {
        var files = db.FileAssets.Include(f => f.UploadedBy).AsQueryable();

        if (actorRole == Role.Employee)
        {
            files = files.Where(f => f.UploadedById == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.ContactId)) files = files.Where(f => f.ContactId == query.ContactId);
        if (!string.IsNullOrWhiteSpace(query.DealId)) files = files.Where(f => f.DealId == query.DealId);
        if (!string.IsNullOrWhiteSpace(query.InvoiceId)) files = files.Where(f => f.InvoiceId == query.InvoiceId);

        var items = await files.OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var file = await db.FileAssets.SingleOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException("File not found");

        try
        {
            await storage.DeleteAsync(file.S3Key, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ExternalServiceException($"File storage is not available: {ex.Message}");
        }

        db.FileAssets.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(string companyId, string fileId, CancellationToken cancellationToken = default)
    {
        var exists = await db.FileAssets.AnyAsync(f => f.Id == fileId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("File not found");
        }

        var versions = await db.FileVersions.Include(v => v.UploadedBy)
            .Where(v => v.FileAssetId == fileId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return versions.Select(v => new FileVersionDto(v.Id, v.VersionNumber, v.Filename, v.MimeType, v.SizeBytes, v.UploadedById, v.UploadedBy.Name, v.CreatedAt)).ToList();
    }

    // Rewrites the FileAsset's pointer columns to the target version's values, after first
    // snapshotting the CURRENT state as a new version - so "restoring" never loses data,
    // it just adds another entry to the history and makes an older one current again. Pure
    // metadata: never calls IFileStorageService (see FileVersion.cs).
    public async Task<FileAssetDto> RestoreVersionAsync(string companyId, string fileId, string versionId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var file = await db.FileAssets.Include(f => f.UploadedBy).SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File not found");

        var target = await db.FileVersions.SingleOrDefaultAsync(v => v.Id == versionId && v.FileAssetId == fileId, cancellationToken)
            ?? throw new NotFoundException("File version not found");

        var latestVersionNumber = await db.FileVersions.Where(v => v.FileAssetId == fileId).MaxAsync(v => v.VersionNumber, cancellationToken);

        db.FileVersions.Add(new FileVersion
        {
            CompanyId = companyId,
            FileAssetId = fileId,
            VersionNumber = latestVersionNumber + 1,
            S3Key = file.S3Key,
            Filename = file.Filename,
            MimeType = file.MimeType,
            SizeBytes = file.SizeBytes,
            UploadedById = actorUserId,
        });

        file.S3Key = target.S3Key;
        file.Filename = target.Filename;
        file.MimeType = target.MimeType;
        file.SizeBytes = target.SizeBytes;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(file);
    }

    private async Task ValidateRelationsAsync(string? contactId, string? dealId, string? invoiceId, CancellationToken cancellationToken)
    {
        if (contactId is null && dealId is null && invoiceId is null)
        {
            throw new ConflictException("At least one of contactId/dealId/invoiceId is required");
        }

        if (contactId is not null && !await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken))
        {
            throw new NotFoundException("Contact not found");
        }
        if (dealId is not null && !await db.Deals.AnyAsync(d => d.Id == dealId, cancellationToken))
        {
            throw new NotFoundException("Deal not found");
        }
        if (invoiceId is not null && !await db.Invoices.AnyAsync(i => i.Id == invoiceId, cancellationToken))
        {
            throw new NotFoundException("Invoice not found");
        }
    }

    private FileAssetDto ToDto(FileAsset f) => new(
        f.Id, f.CompanyId, f.ContactId, f.DealId, f.InvoiceId, f.UploadedById, f.UploadedBy.Name,
        f.Filename, f.MimeType, f.SizeBytes, TryGetDownloadUrl(f.S3Key), f.CreatedAt);

    private string? TryGetDownloadUrl(string s3Key)
    {
        try
        {
            return storage.GetPresignedDownloadUrl(s3Key);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
