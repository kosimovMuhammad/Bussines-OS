using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Metadata-only, deliberately - this environment has no S3 configured (AWS_S3_BUCKET etc.
// unset, see S3FileStorageService), so there is no live object storage to actually copy/
// restore bytes against. A version row here is a snapshot of the FileAsset's pointer
// fields (S3Key/Filename/MimeType/SizeBytes) at a point in time; FilesService.
// RestoreVersionAsync only ever rewrites those columns on the FileAsset row, never touches
// IFileStorageService. Once a real provider is configured, restoring a version would also
// need to make that S3 key the one served for downloads - that's a follow-up, not this phase.
public class FileVersion : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string FileAssetId { get; set; } = string.Empty;
    public FileAsset FileAsset { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public User UploadedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
