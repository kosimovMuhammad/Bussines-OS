namespace BusinessOS.Application.ViewModels.Search;

public record SearchResultItem(string Id, string Type, string Title, string? Subtitle, string Link, double Score);

public record SearchGroupDto(string Type, IReadOnlyList<SearchResultItem> Items, int Total, int Page, int Limit, bool Allowed);

public record SearchResponse(string Query, IReadOnlyList<SearchGroupDto> Groups);
