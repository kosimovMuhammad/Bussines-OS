namespace BusinessOS.Application.ViewModels;

public record ImportErrorDto(int RowNumber, string Message);

// Skip is the default dedup strategy per existing product decision (not error-on-duplicate,
// not overwrite) - a row matching an existing record's natural key (email for
// Contacts/Users, exact title match for Deals/Tasks - see each service's ImportCsvAsync for
// which) is counted in SkippedCount and left untouched, not treated as a failure.
public record ImportResultDto(int ImportedCount, int SkippedCount, IReadOnlyList<ImportErrorDto> Errors);
