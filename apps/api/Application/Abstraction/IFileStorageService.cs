namespace BusinessOS.Application.Abstraction;

public interface IFileStorageService
{
    string BuildKey(string companyId, string filename);
    string GetPresignedUploadUrl(string key, string mimeType);
    string GetPresignedDownloadUrl(string key);
    Task UploadAsync(string key, byte[] content, string mimeType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
