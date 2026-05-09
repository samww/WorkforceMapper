using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Tests;

public class RiskCalculatorTests
{
    // ClassifyApplication boundary tests

    [Fact]
    public void ClassifyApplication_ZeroCoverage_IsHigh()
    {
        Assert.Equal(RiskLevel.High, RiskCalculator.ClassifyApplication(0, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_CoverageBelowMin_IsHigh()
    {
        Assert.Equal(RiskLevel.High, RiskCalculator.ClassifyApplication(0.9, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_CoverageExactlyAtMin_IsMedium()
    {
        Assert.Equal(RiskLevel.Medium, RiskCalculator.ClassifyApplication(1.0, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_CoverageBetweenMinAndRecommended_IsMedium()
    {
        Assert.Equal(RiskLevel.Medium, RiskCalculator.ClassifyApplication(1.5, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_CoverageExactlyAtRecommended_IsLow()
    {
        Assert.Equal(RiskLevel.Low, RiskCalculator.ClassifyApplication(2.0, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_CoverageAboveRecommended_IsLow()
    {
        Assert.Equal(RiskLevel.Low, RiskCalculator.ClassifyApplication(3.0, 1.0, 2.0));
    }

    // Argument validation

    [Fact]
    public void ClassifyApplication_NegativeCoverage_Throws()
    {
        Assert.Throws<ArgumentException>(() => RiskCalculator.ClassifyApplication(-0.1, 1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_NegativeMin_Throws()
    {
        Assert.Throws<ArgumentException>(() => RiskCalculator.ClassifyApplication(0, -1.0, 2.0));
    }

    [Fact]
    public void ClassifyApplication_RecommendedLessThanMin_Throws()
    {
        Assert.Throws<ArgumentException>(() => RiskCalculator.ClassifyApplication(0, 2.0, 1.0));
    }

    // AggregateProductRisk

    [Fact]
    public void AggregateProductRisk_EmptyInput_IsLow()
    {
        Assert.Equal(RiskLevel.Low, RiskCalculator.AggregateProductRisk([]));
    }

    [Fact]
    public void AggregateProductRisk_MixedIncludingHigh_IsHigh()
    {
        Assert.Equal(RiskLevel.High, RiskCalculator.AggregateProductRisk([RiskLevel.High, RiskLevel.Low, RiskLevel.Medium]));
    }

    [Fact]
    public void AggregateProductRisk_AllMedium_IsMedium()
    {
        Assert.Equal(RiskLevel.Medium, RiskCalculator.AggregateProductRisk([RiskLevel.Medium, RiskLevel.Medium]));
    }

    [Fact]
    public void AggregateProductRisk_AllLow_IsLow()
    {
        Assert.Equal(RiskLevel.Low, RiskCalculator.AggregateProductRisk([RiskLevel.Low, RiskLevel.Low]));
    }
}
