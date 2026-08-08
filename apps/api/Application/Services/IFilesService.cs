using BusinessOS.Application.ViewModels.Files;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IFilesService
{
    Task<PresignUploadResponse> PresignAsync(string companyId, PresignUploadRequest request, CancellationToken cancellationToken = default);
    Task<FileAssetDto> ConfirmAsync(string companyId, string uploadedById, ConfirmFileRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileAssetDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryFilesRequest query, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    // Metadata-only (see FileVersion.cs) - no live object storage is configured in this
    // environment, so these never touch IFileStorageService.
    Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(string companyId, string fileId, CancellationToken cancellationToken = default);
    Task<FileAssetDto> RestoreVersionAsync(string companyId, string fileId, string versionId, string actorUserId, CancellationToken cancellationToken = default);
}
