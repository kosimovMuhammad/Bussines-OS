using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Aggregate GET /search is gated by Analytics ("reads broadly across the company", same
// mapping used for Reports) - PermissionAuthorizationFilter fails CLOSED without a
// [RequireModule] on every route, so a class-level default is required. Each /search/{type}
// route below overrides it with the module that actually governs that entity type (same
// module its own direct endpoint uses), so per-type RBAC there works exactly like calling
// /contacts, /deals, etc. directly - a caller without deals.read gets 403 from
// /search/deals, same as from /deals itself.
//
// For the aggregate route, 403-ing the whole call isn't right (a Sales user with no Deals
// permission should still get their Contacts/Tasks hits) - so RBAC there is enforced by
// filtering: SearchController computes the caller's effective permission keys once and
// passes down which of the requested types they may actually read; SearchService returns
// an empty, Allowed=false group for the rest instead of querying for them at all.
//
// Tenant scoping (the real security boundary, independent of RBAC) is enforced inside
// SearchService using currentUser.CompanyId, never anything from the request.
[ApiController]
[Route("search")]
[Authorize]
[RequireModule(ModuleName.Analytics)]
public class SearchController(ISearchService searchService, IPermissionsService permissionsService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? types, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct)
    {
        var requestedTypes = ParseTypes(types);
        var allowedTypes = await GetAllowedTypesAsync(requestedTypes, ct);

        return Ok(await searchService.SearchAsync(currentUser.CompanyId!, RequireQuery(q), requestedTypes, allowedTypes, PageOrDefault(page), LimitOrDefault(limit), ct));
    }

    [HttpGet("contacts")]
    [RequireModule(ModuleName.Contacts)]
    public Task<IActionResult> SearchContacts([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Contacts, q, page, limit, ct);

    [HttpGet("deals")]
    [RequireModule(ModuleName.Deals)]
    public Task<IActionResult> SearchDeals([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Deals, q, page, limit, ct);

    [HttpGet("tasks")]
    [RequireModule(ModuleName.Tasks)]
    public Task<IActionResult> SearchTasks([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Tasks, q, page, limit, ct);

    [HttpGet("files")]
    [RequireModule(ModuleName.Files)]
    public Task<IActionResult> SearchFiles([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Files, q, page, limit, ct);

    [HttpGet("emails")]
    [RequireModule(ModuleName.Communications)]
    public Task<IActionResult> SearchEmails([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Emails, q, page, limit, ct);

    [HttpGet("messages")]
    [RequireModule(ModuleName.Communications)]
    public Task<IActionResult> SearchMessages([FromQuery] string? q, [FromQuery] int page, [FromQuery] int limit, CancellationToken ct) =>
        SearchOneType(SearchTypes.Messages, q, page, limit, ct);

    private async Task<IActionResult> SearchOneType(string type, string? q, int page, int limit, CancellationToken ct) =>
        Ok(await searchService.SearchTypeAsync(currentUser.CompanyId!, type, RequireQuery(q), PageOrDefault(page), LimitOrDefault(limit), ct));

    private async Task<IReadOnlySet<string>> GetAllowedTypesAsync(HashSet<string>? requestedTypes, CancellationToken ct)
    {
        var effective = await permissionsService.GetEffectivePermissionsAsync(currentUser.CompanyId!, currentUser.UserId!, ct);
        var granted = effective.PermissionKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidateTypes = requestedTypes is { Count: > 0 } ? requestedTypes : SearchTypes.All;
        return candidateTypes
            .Where(t => granted.Contains($"{SearchTypes.GoverningModule[t].ToString().ToLowerInvariant()}.read"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string>? ParseTypes(string? types)
    {
        if (string.IsNullOrWhiteSpace(types))
        {
            return null;
        }

        var typeFilter = types
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        var invalid = typeFilter.Except(SearchTypes.All).ToList();
        if (invalid.Count > 0)
        {
            throw new ValidationAppException([$"Unknown search type(s): {string.Join(", ", invalid)}. Valid types: {string.Join(", ", SearchTypes.All)}"]);
        }

        return typeFilter;
    }

    private static string RequireQuery(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            throw new ValidationAppException(["Query parameter 'q' is required"]);
        }

        return q;
    }

    private static int PageOrDefault(int page) => page <= 0 ? 1 : page;

    private static int LimitOrDefault(int limit) => limit <= 0 ? 10 : limit;
}
