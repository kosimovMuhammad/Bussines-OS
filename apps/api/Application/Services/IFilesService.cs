using BusinessOS.Application.ViewModels.Files;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IFilesService
{
    Task<PresignUploadResponse> PresignAsync(string companyId, PresignUploadRequest request, CancellationToken cancellationToken = default);
    Task<FileAssetDto> ConfirmAsync(string companyId, string uploadedById, ConfirmFileRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileAssetDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryFilesRequest query, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
