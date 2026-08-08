namespace BusinessOS.Application.ViewModels.Organization;

public record OrgChartEmployee(string Id, string Name, string? AvatarUrl, string? ManagerId);

public record OrgChartNode(
    string Id,
    string Name,
    string? ManagerId,
    string? ManagerName,
    IReadOnlyList<OrgChartEmployee> Employees,
    IReadOnlyList<OrgChartNode> Children);

public record OrgChartResponse(IReadOnlyList<OrgChartNode> Departments, IReadOnlyList<OrgChartEmployee> UnassignedEmployees);

public record OrgChartPersonSummary(string Id, string Name, string? AvatarUrl);

public record UserOrgChainDto(OrgChartPersonSummary User, IReadOnlyList<OrgChartPersonSummary> ManagementChain, IReadOnlyList<OrgChartPersonSummary> DirectReports);
