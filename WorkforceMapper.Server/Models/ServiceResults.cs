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

public record CapacitySummaryResult(
    int TotalResourceCount,
    decimal TotalMonthlyCost,
    decimal TotalAllocatedMonthlyCost,
    decimal TotalUnallocatedMonthlyCost,
    decimal OverallAllocationPct,
    List<CapacityResult> Divisions,
    List<string> FullyConstrainedDivisions
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
    int TotalResourceCount,
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
    decimal TotalArrUsd,
    List<ProductCostResult> Products
);

public record DonorCandidateRow(
    string ResourceName,
    string ActivityName,
    string ActivityType,
    int Percent,
    decimal ImpliedMonthlyCost,
    double AllocationFte,
    decimal CostPerFte,
    double GapCoveragePct,
    double CumulativeFte,
    double CumulativeGapCoveragePct
);

public record DonorCandidatesResult(
    int ApplicationId,
    string ApplicationName,
    string Division,
    double CurrentCoverageFte,
    double MinSupportFte,
    double RecommendedSupportFte,
    double GapFte,
    int UniqueResourceCount,
    int TotalAllocationCount,
    List<DonorCandidateRow> Candidates
);

public record ProductRiskSummary(
    int HighRiskProductCount,
    int MediumRiskProductCount,
    int LowRiskProductCount,
    decimal TotalHighRiskArrUsd,
    decimal TotalMediumRiskArrUsd,
    List<ProductRiskDto> Products
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
