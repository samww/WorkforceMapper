using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Tests;

public class AllocationValidatorTests
{
    private static Resource ActiveResource() => new()
    {
        Id = 1, ResourceId = "PI00001", Name = "Alice",
        Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1, Jun = 1,
        Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
    };

    private static Resource InactiveJuneResource() => new()
    {
        Id = 2, ResourceId = "PI00002", Name = "Bob",
        Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1, Jun = 0,
        Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
    };

    // --- Cycle 1: Tracer bullet — total exactly 100% is valid ---

    [Fact]
    public void ValidateUpsert_ReturnsValid_WhenTotalExactly100Pct()
    {
        var existing = new List<Mapping>
        {
            new() { ResourceId = 1, ActivityId = 1, Month = "2026-06", Percent = 70 }
        };

        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", existing, activityId: 2, proposedPercent: 30);

        Assert.True(result.IsValid);
        Assert.Equal(RejectionReason.None, result.Reason);
    }

    // --- Cycle 2: New mapping pushes total to 101% ---

    [Fact]
    public void ValidateUpsert_ReturnsWouldExceedCap_WhenTotalExceeds100Pct()
    {
        var existing = new List<Mapping>
        {
            new() { ResourceId = 1, ActivityId = 1, Month = "2026-06", Percent = 100 }
        };

        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", existing, activityId: 2, proposedPercent: 1);

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.WouldExceedCap, result.Reason);
    }

    // --- Cycle 3: Replacing 50% with 60% pushes total over cap ---

    [Fact]
    public void ValidateUpsert_ReturnsWouldExceedCap_WhenReplacingMappingPushesOver()
    {
        var existing = new List<Mapping>
        {
            new() { ResourceId = 1, ActivityId = 1, Month = "2026-06", Percent = 50 },
            new() { ResourceId = 1, ActivityId = 2, Month = "2026-06", Percent = 50 }
        };

        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", existing, activityId: 2, proposedPercent: 60);

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.WouldExceedCap, result.Reason);
    }

    // --- Cycle 4: Replacing 50% with 30% reduces total, still valid ---

    [Fact]
    public void ValidateUpsert_ReturnsValid_WhenReplacingMappingReducesTotal()
    {
        var existing = new List<Mapping>
        {
            new() { ResourceId = 1, ActivityId = 1, Month = "2026-06", Percent = 50 },
            new() { ResourceId = 1, ActivityId = 2, Month = "2026-06", Percent = 50 }
        };

        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", existing, activityId: 2, proposedPercent: 30);

        Assert.True(result.IsValid);
        Assert.Equal(RejectionReason.None, result.Reason);
    }

    // --- Cycle 5: Negative percent rejected as WouldExceedCap ---

    [Fact]
    public void ValidateUpsert_ReturnsWouldExceedCap_WhenProposedPercentNegative()
    {
        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", [], activityId: 1, proposedPercent: -10);

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.WouldExceedCap, result.Reason);
    }

    // --- Cycle 6: Inactive-month upsert rejected ---

    [Fact]
    public void ValidateUpsert_ReturnsResourceInactiveInMonth_WhenResourceInactiveInMonth()
    {
        var result = AllocationValidator.ValidateUpsert(
            InactiveJuneResource(), "2026-06", [], activityId: 1, proposedPercent: 50);

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.ResourceInactiveInMonth, result.Reason);
    }

    // --- Cycle 7: Inactive-month delete rejected ---

    [Fact]
    public void ValidateDelete_ReturnsResourceInactiveInMonth_WhenResourceInactiveInMonth()
    {
        var result = AllocationValidator.ValidateDelete(InactiveJuneResource(), "2026-06");

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.ResourceInactiveInMonth, result.Reason);
    }

    // --- Cycle 8: Zero percent rejected as WouldExceedCap (ADR 8) ---

    [Fact]
    public void ValidateUpsert_ReturnsWouldExceedCap_WhenPercentIsZero()
    {
        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", [], activityId: 1, proposedPercent: 0);

        Assert.False(result.IsValid);
        Assert.Equal(RejectionReason.WouldExceedCap, result.Reason);
    }

    // --- Cycle 9: Idempotent replace — same percent leaves total unchanged ---

    [Fact]
    public void ValidateUpsert_ReturnsValid_WhenReplacingMappingWithSamePercent()
    {
        var existing = new List<Mapping>
        {
            new() { ResourceId = 1, ActivityId = 1, Month = "2026-06", Percent = 70 },
            new() { ResourceId = 1, ActivityId = 2, Month = "2026-06", Percent = 30 }
        };

        var result = AllocationValidator.ValidateUpsert(
            ActiveResource(), "2026-06", existing, activityId: 2, proposedPercent: 30);

        Assert.True(result.IsValid);
        Assert.Equal(RejectionReason.None, result.Reason);
    }
}
