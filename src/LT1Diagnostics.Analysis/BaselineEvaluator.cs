using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Domain.Diagnostics;

namespace LT1Diagnostics.Analysis;

public static class BaselineEvaluator
{
    public static BaselineComparison Compare(double observedValue, ConditionMatchedBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (baseline.VerificationStatus != VerificationStatus.Verified)
        {
            return NotEvaluated(observedValue, baseline, "The matching baseline has not been verified, so no normal/abnormal conclusion was produced.");
        }

        if (baseline.Minimum > baseline.Maximum)
        {
            return NotEvaluated(observedValue, baseline, "The matching baseline is invalid because its minimum exceeds its maximum.");
        }

        if (observedValue >= baseline.Minimum && observedValue <= baseline.Maximum)
        {
            return new BaselineComparison(
                baseline.SignalId,
                baseline.ConditionId,
                observedValue,
                baseline.Unit,
                BaselineComparisonStatus.WithinRange,
                0,
                0,
                "The observed value is within the verified condition-matched range.");
        }

        bool below = observedValue < baseline.Minimum;
        double boundary = below ? baseline.Minimum : baseline.Maximum;
        double variance = observedValue - boundary;
        double? percent = boundary == 0 ? null : (variance / Math.Abs(boundary)) * 100;
        return new BaselineComparison(
            baseline.SignalId,
            baseline.ConditionId,
            observedValue,
            baseline.Unit,
            below ? BaselineComparisonStatus.BelowRange : BaselineComparisonStatus.AboveRange,
            variance,
            percent,
            below
                ? "The observed value is below the verified condition-matched range."
                : "The observed value is above the verified condition-matched range.");
    }

    private static BaselineComparison NotEvaluated(
        double observedValue,
        ConditionMatchedBaseline baseline,
        string explanation) =>
        new(
            baseline.SignalId,
            baseline.ConditionId,
            observedValue,
            baseline.Unit,
            BaselineComparisonStatus.NotEvaluated,
            null,
            null,
            explanation);
}
