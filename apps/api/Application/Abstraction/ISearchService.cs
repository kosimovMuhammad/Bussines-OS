using BusinessOS.Application.ViewModels.Search;

namespace BusinessOS.Application.Abstraction;

// Implemented in Infrastructure (BusinessOS.Infrastructure.Search.SearchService), not
// Application - it's built on raw Postgres trigram SQL (Database.SqlQuery, a relational-
// only EF extension), which requires the Npgsql/Relational packages that only
// Infrastructure references. Same reasoning as IFileStorageService/IPaymentProvider
// living here: a persistence/external-system boundary, not business logic.
public interface ISearchService
{
    // types: null/empty means "all types". Every entry must be one of the implementation's
    // known valid type names - the caller (controller) is responsible for rejecting
    // unknown type names with a 4xx before reaching here.
    //
    // allowedTypes: the subset of `types` the caller's RBAC grants Read on (computed by the
    // controller from the caller's effective permissions). A requested type NOT in
    // allowedTypes still gets a group in the response (Allowed=false, Items empty) rather
    // than being silently dropped or 403ing the whole call - so a Sales user with no Deals
    // permission calling /search still gets their Contacts/Tasks results back.
    Task<SearchResponse> SearchAsync(string companyId, string query, IReadOnlySet<string>? types, IReadOnlySet<string> allowedTypes, int page, int limit, CancellationToken cancellationToken = default);

    // Single-type search backing /search/{type} - the caller is responsible for the RBAC
    // check (that route's [RequireModule] already 403s before this is reached, so there's
    // no "allowed" concept to thread through here).
    Task<SearchGroupDto> SearchTypeAsync(string companyId, string type, string query, int page, int limit, CancellationToken cancellationToken = default);
}
