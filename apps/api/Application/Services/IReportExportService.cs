using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IReportExportService
{
    Task<string> ExportCsvAsync(
        string companyId,
        string type,
        string requestingUserId,
        Role requestingUserRole,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
