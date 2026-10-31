using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace BusinessOS.Infrastructure.Files;

// Development-only stand-in for S3FileStorageService (see DependencyInjection.AddInfrastructure)
// so local dev works with zero external dependencies - no AWS credentials, no MinIO/Docker.
// "Presigned" URLs point back at this same API: PUT goes to LocalUploadController
// (dev/local-upload/{key}), GET is served by the /uploads static file middleware wired up in
// Program.cs. Never registered outside Development.
public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _uploadsRoot;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalDiskFileStorageService(IHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _uploadsRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_uploadsRoot);
    }

    public string BuildKey(string companyId, string filename) =>
        $"{companyId}/{Guid.NewGuid()}-{filename}";

    public string GetPresignedUploadUrl(string key, string mimeType) =>
        $"{BaseUrl()}/dev/local-upload/{EncodeKeyForUrl(key)}";

    public string GetPresignedDownloadUrl(string key) =>
        $"{BaseUrl()}/uploads/{EncodeKeyForUrl(key)}";

    public async Task UploadAsync(string key, byte[] content, string mimeType, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    // Guards against path traversal via a crafted key/filename (e.g. "../../etc/passwd") -
    // the resolved path must stay inside _uploadsRoot.
    private string ResolvePath(string key)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_uploadsRoot, key));
        if (!fullPath.StartsWith(_uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Invalid file key");
        }
        return fullPath;
    }

    private static string EncodeKeyForUrl(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    private string BaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("LocalDiskFileStorageService requires an active HTTP request to build an absolute URL");
        return $"{request.Scheme}://{request.Host}";
    }
}
