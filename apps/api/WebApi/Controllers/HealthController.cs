using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController(DataContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        return Ok(new { status = "ok" });
    }
}
