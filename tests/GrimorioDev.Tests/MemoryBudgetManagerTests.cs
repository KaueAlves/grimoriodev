using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class MemoryBudgetManagerTests
{
    private readonly MemoryBudgetManager _sut;

    public MemoryBudgetManagerTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryBudgetManager>>();
        _sut = new MemoryBudgetManager(logger);
    }

    [Fact]
    public void BudgetBytes_ShouldBePositive()
    {
        _sut.BudgetBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void RecordAllocation_IncreasesUsage()
    {
        _sut.RecordAllocation(1024);
        _sut.CurrentUsageBytes.ShouldBe(1024);
        _sut.RecordAllocation(2048);
        _sut.CurrentUsageBytes.ShouldBe(3072);
    }

    [Fact]
    public void RecordDeallocation_DecreasesUsage()
    {
        _sut.RecordAllocation(5000);
        _sut.RecordDeallocation(2000);
        _sut.CurrentUsageBytes.ShouldBe(3000);
    }

    [Fact]
    public void UsagePercentage_ShouldBeZero_Initially()
    {
        _sut.UsagePercentage.ShouldBe(0);
    }

    [Fact]
    public void IsUnderPressure_ShouldBeFalse_Initially()
    {
        _sut.IsUnderPressure.ShouldBeFalse();
    }

    [Fact]
    public void IsCritical_ShouldBeFalse_Initially()
    {
        _sut.IsCritical.ShouldBeFalse();
    }

    [Fact]
    public void RecordAllocation_Negative_ShouldNotThrow()
    {
        Should.NotThrow(() => _sut.RecordAllocation(-100));
    }

    [Fact]
    public void RecordDeallocation_Negative_ShouldNotThrow()
    {
        Should.NotThrow(() => _sut.RecordDeallocation(-100));
    }

    [Fact]
    public void Update_ShouldRecalculate()
    {
        _sut.RecordAllocation(100_000_000);
        _sut.Update();
        _sut.UsagePercentage.ShouldBeGreaterThan(0);
    }
}
