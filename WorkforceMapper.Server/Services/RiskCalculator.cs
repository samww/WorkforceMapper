namespace WorkforceMapper.Server.Services;

public enum RiskLevel { Low, Medium, High }

public static class RiskCalculator
{
    public static RiskLevel ClassifyApplication(double coverage, double min, double recommended)
    {
        if (coverage < 0) throw new ArgumentException("coverage must be >= 0", nameof(coverage));
        if (min < 0) throw new ArgumentException("min must be >= 0", nameof(min));
        if (recommended < min) throw new ArgumentException("recommended must be >= min", nameof(recommended));

        if (coverage < min) return RiskLevel.High;
        if (coverage < recommended) return RiskLevel.Medium;
        return RiskLevel.Low;
    }

    public static RiskLevel AggregateProductRisk(IEnumerable<RiskLevel> appRisks)
    {
        var result = RiskLevel.Low;
        foreach (var r in appRisks)
            if (r > result) result = r;
        return result;
    }
}
