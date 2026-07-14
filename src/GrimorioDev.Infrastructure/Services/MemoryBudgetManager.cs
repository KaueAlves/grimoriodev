using GrimorioDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public sealed class MemoryBudgetManager : IMemoryBudgetManager
{
    private readonly ILogger<MemoryBudgetManager> _logger;
    private long _currentUsage;
    private long _budgetBytes;

    public MemoryBudgetManager(ILogger<MemoryBudgetManager> logger)
    {
        _logger = logger;
        _budgetBytes = CalculateBudget();
    }

    public long BudgetBytes => _budgetBytes;
    public long CurrentUsageBytes => Interlocked.Read(ref _currentUsage);
    public double UsagePercentage => _budgetBytes > 0 ? (double)CurrentUsageBytes / _budgetBytes * 100 : 0;
    public bool IsUnderPressure => UsagePercentage > 70;
    public bool IsCritical => UsagePercentage > 90;

    public void RecordAllocation(long bytes)
    {
        Interlocked.Add(ref _currentUsage, bytes);
    }

    public void RecordDeallocation(long bytes)
    {
        Interlocked.Add(ref _currentUsage, -bytes);
    }

    public void Update()
    {
        _budgetBytes = CalculateBudget();
        var usage = CurrentUsageBytes;
        var budget = _budgetBytes;
        var pct = budget > 0 ? (double)usage / budget * 100 : 0;

        _logger.LogDebug("Memory budget: {Usage:N0} / {Budget:N0} bytes ({Pct:F1}%)",
            usage, budget, pct);

        if (pct > 95)
            _logger.LogWarning("Memory critical: {Pct:F1}% of budget used", pct);
        else if (pct > 80)
            _logger.LogWarning("Memory pressure: {Pct:F1}% of budget used", pct);
    }

    private static long CalculateBudget()
    {
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var budget30Percent = (long)(totalMemory * 0.3);
        var budgetMax500Mb = 500L * 1024 * 1024;
        return Math.Min(budget30Percent, budgetMax500Mb);
    }
}
