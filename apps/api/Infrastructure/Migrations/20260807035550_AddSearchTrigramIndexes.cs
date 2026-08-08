using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Hand-written raw SQL (no EF model change involved) - GIN trigram indexes backing
    // the /search endpoint's ILIKE + similarity() queries (Search & Reports Phase 1).
    // pg_trgm chosen over tsvector/FTS: short identifier-like fields (names/emails/
    // phones/titles), small per-tenant data volume, users typing partial/fuzzy fragments.
    public partial class AddSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE INDEX IF NOT EXISTS ix_contacts_search_trgm ON contacts USING GIN (
                    (coalesce(""firstName"", '') || ' ' || coalesce(""lastName"", '') || ' ' || coalesce(""email"", '') || ' ' || coalesce(""phone"", '')) gin_trgm_ops
                );

                CREATE INDEX IF NOT EXISTS ix_deals_search_trgm ON deals USING GIN (""title"" gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ix_tasks_search_trgm ON tasks USING GIN (""title"" gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ix_users_search_trgm ON users USING GIN (
                    (coalesce(""name"", '') || ' ' || coalesce(""email"", '')) gin_trgm_ops
                );
            ");
        }

        /// <inheritdoc />
        // Extension is deliberately left in place here - it's a shared, global resource
        // other features may also rely on; dropping it is out of scope for this revert.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_contacts_search_trgm;
                DROP INDEX IF EXISTS ix_deals_search_trgm;
                DROP INDEX IF EXISTS ix_tasks_search_trgm;
                DROP INDEX IF EXISTS ix_users_search_trgm;
            ");
        }
    }
}
