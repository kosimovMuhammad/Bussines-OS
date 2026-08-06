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
        var url = storage.GetPresignedUploadUrl(key, request.MimeType);
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

        await storage.DeleteAsync(file.S3Key, cancellationToken);

        db.FileAssets.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
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
