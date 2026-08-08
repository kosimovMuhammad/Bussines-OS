using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Search;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Search;

// pg_trgm-backed search (GIN indexes added in the AddSearchTrigramIndexes and
// AddSearchTrigramIndexesForFilesAndCommunications migrations) - see the Search & Reports
// Phase 1 plan for why trigram was chosen over tsvector/FTS for this codebase's short
// identifier fields (names/emails/phones/titles/filenames) and small per-tenant data
// volumes. WHERE uses a hybrid of the "%" similarity operator (typo tolerance - "Jhon"
// still matches "John" once its trigram similarity clears pg_trgm.similarity_threshold,
// default 0.3) OR a plain ILIKE (so short/partial exact substrings that don't share enough
// trigrams to pass the threshold still match). Both sides of the OR can use the same GIN
// trgm index.
//
// SECURITY: every query here is raw SQL, which bypasses EF's LINQ-based tenant
// HasQueryFilter entirely - "companyId" must be included explicitly in every WHERE clause
// below, and companyId itself must only ever come from the authenticated caller's own
// tenant (never from client-supplied input) - see SearchController, which passes
// currentUser.CompanyId, not anything from the query string.
public class SearchService(IAppDbContext db) : ISearchService
{
    public async Task<SearchResponse> SearchAsync(string companyId, string query, IReadOnlySet<string>? types, IReadOnlySet<string> allowedTypes, int page, int limit, CancellationToken cancellationToken = default)
    {
        var activeTypes = types is { Count: > 0 } ? types : SearchTypes.All;
        var groups = new List<SearchGroupDto>();

        foreach (var type in SearchTypes.All.Where(activeTypes.Contains))
        {
            if (!allowedTypes.Contains(type))
            {
                groups.Add(new SearchGroupDto(type, [], 0, page, limit, Allowed: false));
                continue;
            }

            groups.Add(await SearchTypeAsync(companyId, type, query, page, limit, cancellationToken));
        }

        return new SearchResponse(query, groups);
    }

    public async Task<SearchGroupDto> SearchTypeAsync(string companyId, string type, string query, int page, int limit, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 50);
        var offset = (page - 1) * limit;
        var pattern = $"%{EscapeLikePattern(query.Trim())}%";

        var rows = type.ToLowerInvariant() switch
        {
            SearchTypes.Contacts => await SearchContactsAsync(companyId, query, pattern, limit, offset, cancellationToken),
            SearchTypes.Deals => await SearchDealsAsync(companyId, query, pattern, limit, offset, cancellationToken),
            SearchTypes.Tasks => await SearchTasksAsync(companyId, query, pattern, limit, offset, cancellationToken),
            SearchTypes.Users => await SearchUsersAsync(companyId, query, pattern, limit, offset, cancellationToken),
            SearchTypes.Files => await SearchFilesAsync(companyId, query, pattern, limit, offset, cancellationToken),
            SearchTypes.Emails => await SearchCommunicationsAsync(companyId, query, pattern, emails: true, limit, offset, cancellationToken),
            SearchTypes.Messages => await SearchCommunicationsAsync(companyId, query, pattern, emails: false, limit, offset, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown search type"),
        };

        var total = rows.Count > 0 ? rows[0].Total : 0;
        var items = rows.Select(r => ToItem(r, type)).ToList();
        return new SearchGroupDto(type, items, total, page, limit, Allowed: true);
    }

    private Task<List<SearchHitRow>> SearchContactsAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   trim(coalesce("firstName", '') || ' ' || coalesce("lastName", '')) AS "Title",
                   coalesce("email", "phone") AS "Subtitle",
                   similarity(coalesce("firstName", '') || ' ' || coalesce("lastName", '') || ' ' || coalesce("email", '') || ' ' || coalesce("phone", ''), {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM contacts
            WHERE "companyId" = {companyId}
              AND ((coalesce("firstName", '') || ' ' || coalesce("lastName", '') || ' ' || coalesce("email", '') || ' ' || coalesce("phone", '')) % {query}
                   OR (coalesce("firstName", '') || ' ' || coalesce("lastName", '') || ' ' || coalesce("email", '') || ' ' || coalesce("phone", '')) ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private Task<List<SearchHitRow>> SearchDealsAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   "title" AS "Title",
                   CAST(NULL AS TEXT) AS "Subtitle",
                   similarity("title", {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM deals
            WHERE "companyId" = {companyId}
              AND ("title" % {query} OR "title" ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private Task<List<SearchHitRow>> SearchTasksAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   "title" AS "Title",
                   CAST(NULL AS TEXT) AS "Subtitle",
                   similarity("title", {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM tasks
            WHERE "companyId" = {companyId}
              AND ("title" % {query} OR "title" ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private Task<List<SearchHitRow>> SearchUsersAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   "name" AS "Title",
                   "email" AS "Subtitle",
                   similarity(coalesce("name", '') || ' ' || coalesce("email", ''), {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM users
            WHERE "companyId" = {companyId}
              AND ((coalesce("name", '') || ' ' || coalesce("email", '')) % {query}
                   OR (coalesce("name", '') || ' ' || coalesce("email", '')) ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private Task<List<SearchHitRow>> SearchFilesAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   "filename" AS "Title",
                   "mimeType" AS "Subtitle",
                   similarity("filename", {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM file_assets
            WHERE "companyId" = {companyId}
              AND ("filename" % {query} OR "filename" ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    // CommunicationChannel enum stores as its int value (see DataContext) - Email = 1.
    // "emails" = channel 1, "messages" = everything else (Whatsapp/ManualNote/PhoneCall).
    private Task<List<SearchHitRow>> SearchCommunicationsAsync(string companyId, string query, string pattern, bool emails, int limit, int offset, CancellationToken cancellationToken)
    {
        return emails ? SearchCommunicationsEmailAsync(companyId, query, pattern, limit, offset, cancellationToken)
            : SearchCommunicationsMessageAsync(companyId, query, pattern, limit, offset, cancellationToken);
    }

    // Two separate static query bodies rather than interpolating the channel condition as
    // a hole - a FormattableString hole always becomes a bound parameter, never raw SQL
    // text, so interpolating "channel" = 1" as a hole sent the *string* as a parameter
    // value instead of embedding it as SQL (42804: Postgres tried to AND a text value).
    private Task<List<SearchHitRow>> SearchCommunicationsEmailAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   left("content", 80) AS "Title",
                   to_char("timestamp", 'YYYY-MM-DD"T"HH24:MI:SS"Z"') AS "Subtitle",
                   similarity("content", {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM communications
            WHERE "companyId" = {companyId}
              AND "channel" = 1
              AND ("content" % {query} OR "content" ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private Task<List<SearchHitRow>> SearchCommunicationsMessageAsync(string companyId, string query, string pattern, int limit, int offset, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<SearchHitRow>($"""
            SELECT "id" AS "Id",
                   left("content", 80) AS "Title",
                   to_char("timestamp", 'YYYY-MM-DD"T"HH24:MI:SS"Z"') AS "Subtitle",
                   similarity("content", {query}) AS "Rank",
                   count(*) OVER() AS "Total"
            FROM communications
            WHERE "companyId" = {companyId}
              AND "channel" <> 1
              AND ("content" % {query} OR "content" ILIKE {pattern})
            ORDER BY "Rank" DESC
            LIMIT {limit} OFFSET {offset}
            """).ToListAsync(cancellationToken);

    private static SearchResultItem ToItem(SearchHitRow row, string type) =>
        new(row.Id, type, row.Title, row.Subtitle, LinkFor(type, row.Id), row.Rank);

    private static string LinkFor(string type, string id) => type switch
    {
        SearchTypes.Emails or SearchTypes.Messages => $"/communications/{id}",
        _ => $"/{type}/{id}",
    };

    // Escapes LIKE/ILIKE metacharacters in user input so a query containing "%" or "_"
    // doesn't silently turn into an unintended wildcard pattern. Postgres's default LIKE
    // escape character is backslash, so no ESCAPE clause is needed once this runs.
    public static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private class SearchHitRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public double Rank { get; set; }
        public int Total { get; set; }
    }
}
