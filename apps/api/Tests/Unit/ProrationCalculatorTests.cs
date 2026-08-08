using BusinessOS.Application.Services;

namespace BusinessOS.Tests.Unit;

public class ProrationCalculatorTests
{
    [Fact]
    public void CalculateProratedAmount_HalfwayThroughPeriod_ChargesHalfTheDifference()
    {
        var periodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc);

        var amount = ProrationCalculator.CalculateProratedAmount(650m, 1500m, periodStart, periodEnd, now);

        Assert.Equal(425m, amount);
    }

    [Fact]
    public void CalculateProratedAmount_AtPeriodStart_ChargesFullDifference()
    {
        var periodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        var amount = ProrationCalculator.CalculateProratedAmount(650m, 1500m, periodStart, periodEnd, periodStart);

        Assert.Equal(850m, amount);
    }

    [Fact]
    public void CalculateProratedAmount_AtPeriodEnd_ChargesNothing()
    {
        var periodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        var amount = ProrationCalculator.CalculateProratedAmount(650m, 1500m, periodStart, periodEnd, periodEnd);

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void CalculateProratedAmount_NowPastPeriodEnd_ClampsToZeroRemaining()
    {
        var periodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = periodEnd.AddDays(5);

        var amount = ProrationCalculator.CalculateProratedAmount(650m, 1500m, periodStart, periodEnd, now);

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void CalculateProratedAmount_ZeroLengthPeriod_ReturnsZero()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var amount = ProrationCalculator.CalculateProratedAmount(650m, 1500m, now, now, now);

        Assert.Equal(0m, amount);
    }
}
