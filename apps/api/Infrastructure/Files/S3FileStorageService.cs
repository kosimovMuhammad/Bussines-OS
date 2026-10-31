using Amazon.S3;
using Amazon.S3.Model;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.Files;

public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3? _client;
    private readonly string? _bucket;

    public S3FileStorageService(IConfiguration configuration)
    {
        _bucket = configuration["AWS_S3_BUCKET"];
        var accessKeyId = configuration["AWS_ACCESS_KEY_ID"];
        var secretAccessKey = configuration["AWS_SECRET_ACCESS_KEY"];
        var region = configuration["AWS_REGION"] ?? "us-east-1";
        var endpointUrl = configuration["AWS_S3_ENDPOINT_URL"];

        if (!string.IsNullOrEmpty(_bucket) && !string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
        {
            if (string.IsNullOrEmpty(endpointUrl))
            {
                _client = new AmazonS3Client(accessKeyId, secretAccessKey, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                // S3-compatible endpoint override for local dev (e.g. MinIO), which requires
                // path-style bucket addressing instead of AWS's default virtual-hosted style.
                var config = new AmazonS3Config
                {
                    ServiceURL = endpointUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = region,
                };
                _client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
            }
        }
    }

    public string BuildKey(string companyId, string filename) =>
        $"{companyId}/{Guid.NewGuid()}-{filename}";

    public string GetPresignedUploadUrl(string key, string mimeType)
    {
        var client = RequireClient();
        return client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = mimeType,
            Expires = DateTime.UtcNow.AddSeconds(900),
        });
    }

    public string GetPresignedDownloadUrl(string key)
    {
        var client = RequireClient();
        return client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddSeconds(900),
        });
    }

    public async Task UploadAsync(string key, byte[] content, string mimeType, CancellationToken cancellationToken = default)
    {
        var client = RequireClient();
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = new MemoryStream(content),
            ContentType = mimeType,
        }, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var client = RequireClient();
        await client.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    private IAmazonS3 RequireClient() =>
        _client ?? throw new InvalidOperationException("AWS S3 is not configured (AWS_S3_BUCKET/AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY)");
}
