using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Domain.Diagnostics;

namespace LT1Diagnostics.Analysis.Tests;

public sealed class TransmissionSessionAnalyzerTests
{
    [Fact]
    public void ReportsObservedRangesAndCommandChangesWithoutCallingThemFaults()
    {
        TransmissionObservation[] observations =
        [
            CreateObservation(TimeSpan.Zero, 800, 0, 1, 180),
            CreateObservation(TimeSpan.FromSeconds(1), 1400, 12, 2, 90),
            CreateObservation(TimeSpan.FromSeconds(2), 1800, 25, 3, 40),
        ];

        TransmissionSessionAnalysis result = TransmissionSessionAnalyzer.Analyze(observations);

        Assert.Equal(3, result.SampleCount);
        Assert.Equal(new ObservedRange(800, 1800), result.EngineSpeedRpm);
        Assert.Equal(new ObservedRange(0, 25), result.VehicleSpeedMph);
        Assert.Equal(2, result.Events.Count(item => item.Kind == TransmissionEventKind.CommandedGearChanged));
        Assert.Contains("not repair conclusions", result.InterpretationBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public void UnverifiedBaselineCannotProduceAnAbnormalConclusion()
    {
        var baseline = new ConditionMatchedBaseline(
            "transmission.fluid-temperature",
            "warm-idle",
            "degC",
            70,
            95,
            VerificationStatus.Unverified,
            0,
            ["placeholder"]);

        BaselineComparison result = BaselineEvaluator.Compare(120, baseline);

        Assert.Equal(BaselineComparisonStatus.NotEvaluated, result.Status);
        Assert.Null(result.VariancePercent);
    }

    [Fact]
    public void VerifiedBaselineQuantifiesVarianceFromNearestBoundary()
    {
        var baseline = new ConditionMatchedBaseline(
            "test.signal",
            "test-condition",
            "unit",
            10,
            20,
            VerificationStatus.Verified,
            20,
            ["verified-source"]);

        BaselineComparison result = BaselineEvaluator.Compare(25, baseline);

        Assert.Equal(BaselineComparisonStatus.AboveRange, result.Status);
        Assert.Equal(5, result.VarianceFromNearestBoundary);
        Assert.Equal(25, result.VariancePercent);
    }

    private static TransmissionObservation CreateObservation(
        TimeSpan elapsed,
        double rpm,
        double speed,
        int gear,
        double slip) =>
        new(
            elapsed,
            rpm,
            speed,
            gear,
            slip,
            50,
            13.5,
            90,
            1.5,
            1.5,
            false,
            false,
            false,
            false,
            VerificationStatus.Unverified);
}
