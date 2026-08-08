namespace BusinessOS.Application.ViewModels.Billing;

public record UsageMetricDto(string MetricKey, int CurrentValue, int? Limit, bool IsUnlimited);
