using BusinessOS.Application.Abstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Development-only replacement for "PUT to an S3 presigned URL" (see LocalDiskFileStorageService).
// AllowAnonymous to mirror real presigned-URL semantics - the S3 flow doesn't require a JWT
// either, the unguessable key stands in for the signature. 404s outright in Production/Staging
// so this can never accidentally activate outside local dev, regardless of which
// IFileStorageService is registered.
[ApiController]
[Route("dev/local-upload")]
[AllowAnonymous]
public class LocalUploadController(IFileStorageService storage, IWebHostEnvironment env) : ControllerBase
{
    [HttpPut("{*key}")]
    public async Task<IActionResult> Upload(string key, CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);

        await storage.UploadAsync(key, buffer.ToArray(), Request.ContentType ?? "application/octet-stream", ct);
        return Ok(new { success = true, key });
    }
}
