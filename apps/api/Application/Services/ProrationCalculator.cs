namespace BusinessOS.Application.Services;

// Pure calculation, no I/O - actual billing/collection is deferred to the payment
// provider integration (Billing Phase 7). For now the prorated amount is computed and
// surfaced to the caller/audit log so the eventual invoicing job has a real figure to
// work from, rather than inventing one later.
public static class ProrationCalculator
{
    public static decimal CalculateProratedAmount(decimal oldPrice, decimal newPrice, DateTime periodStart, DateTime periodEnd, DateTime now)
    {
        var totalDays = (periodEnd - periodStart).TotalDays;
        if (totalDays <= 0)
        {
            return 0m;
        }

        var remainingDays = Math.Clamp((periodEnd - now).TotalDays, 0, totalDays);
        var fractionRemaining = (decimal)(remainingDays / totalDays);

        return Math.Round((newPrice - oldPrice) * fractionRemaining, 2);
    }
}
