namespace WorkforceMapper.Server.Models;

public record CapacityResult(
    string Division,
    int ResourceCount,
    Dictionary<string, int> ResourceTypeSplit,
    decimal MonthlyCost,
    decimal AllocatedMonthlyCost,
    decimal UnallocatedMonthlyCost,
    decimal AllocationPct
);

public record AllocationResult(
    int ActivityId,
    string ActivityName,
    string ActivityType,
    int ResourceDbId,
    string ResourceName,
    string Division,
    decimal Percent,
    decimal ImpliedMonthlyCost
);

public record UnallocatedResult(
    string ResourceId,
    string ResourceName,
    string Division,
    string ResourceType,
    decimal MonthlyRate,
    decimal AllocationPct,
    decimal IdleMonthlyCost
);

public record ProductCostResult(
    int? ProductId,
    string ProductName,
    decimal ArrUsd,
    decimal TotalAllocatedCost
);
