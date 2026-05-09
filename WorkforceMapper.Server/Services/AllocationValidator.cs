using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public enum RejectionReason { None, WouldExceedCap, ResourceInactiveInMonth }

public readonly record struct ValidationResult(bool IsValid, RejectionReason Reason);

public static class AllocationValidator
{
    public static ValidationResult ValidateUpsert(
        Resource resource,
        string month,
        IReadOnlyCollection<Mapping> currentMappingsForResourceMonth,
        int activityId,
        int proposedPercent)
    {
        if (!resource.IsActiveIn(month))
            return new ValidationResult(false, RejectionReason.ResourceInactiveInMonth);

        if (proposedPercent < 1)
            return new ValidationResult(false, RejectionReason.WouldExceedCap);

        var total = currentMappingsForResourceMonth
            .Where(m => m.ActivityId != activityId)
            .Sum(m => m.Percent)
            + proposedPercent;

        if (total > 100)
            return new ValidationResult(false, RejectionReason.WouldExceedCap);

        return new ValidationResult(true, RejectionReason.None);
    }

    public static ValidationResult ValidateDelete(Resource resource, string month)
    {
        if (!resource.IsActiveIn(month))
            return new ValidationResult(false, RejectionReason.ResourceInactiveInMonth);

        return new ValidationResult(true, RejectionReason.None);
    }
}
