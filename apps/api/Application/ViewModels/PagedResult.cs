namespace BusinessOS.Application.ViewModels;

public record PagedResult<T>(IReadOnlyList<T> Data, int Page, int Limit, int Total);
