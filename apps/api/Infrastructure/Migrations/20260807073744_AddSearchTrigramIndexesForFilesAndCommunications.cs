using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Extends the Search & Reports Phase 1 trigram indexes to the two search types added in
    // Phase 3 (/search/files, /search/emails, /search/messages). pg_trgm is already enabled
    // (see AddSearchTrigramIndexes) - just the two new GIN indexes here.
    public partial class AddSearchTrigramIndexesForFilesAndCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_file_assets_search_trgm ON file_assets USING GIN (""filename"" gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ix_communications_search_trgm ON communications USING GIN (""content"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_file_assets_search_trgm;
                DROP INDEX IF EXISTS ix_communications_search_trgm;
            ");
        }
    }
}
