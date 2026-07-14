namespace GrimorioDev.Domain.Interfaces;

public interface IMemoryBudgetManager
{
    long BudgetBytes { get; }
    long CurrentUsageBytes { get; }
    double UsagePercentage { get; }
    bool IsUnderPressure { get; }
    bool IsCritical { get; }
    void RecordAllocation(long bytes);
    void RecordDeallocation(long bytes);
    void Update();
}
