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
    int Percent,
    decimal ImpliedMonthlyCost
);

public record ActivityAllocationsResult(
    int ResourceCount,
    decimal TotalMonthlyCost,
    List<AllocationResult> Allocations
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

public record UnallocatedDivisionGroup(
    string Division,
    int ResourceCount,
    decimal TotalIdleMonthlyCost,
    List<UnallocatedResult> Resources
);

public record UnallocatedResourcesResult(
    decimal TotalIdleMonthlyCost,
    List<UnallocatedDivisionGroup> Divisions
);

public record ProductCostResult(
    int? ProductId,
    string ProductName,
    decimal ArrUsd,
    decimal TotalAllocatedCost
);

public record ProductCostSummary(
    decimal TotalPayroll,
    decimal TotalMappedCost,
    List<ProductCostResult> Products
);

public record OverallocatedResult(
    string ResourceId,
    string Name,
    string Company,
    string ResourceType,
    string Division,
    decimal AllocationPct,
    decimal OverallocationPct
);
