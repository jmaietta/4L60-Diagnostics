using LT1Diagnostics.Domain.Definitions;

namespace LT1Diagnostics.Domain.Diagnostics;

public sealed record ConditionMatchedBaseline(
    string SignalId,
    string ConditionId,
    string Unit,
    double Minimum,
    double Maximum,
    VerificationStatus VerificationStatus,
    int SampleCount,
    IReadOnlyList<string> SourceReferences);

public enum BaselineComparisonStatus
{
    WithinRange,
    BelowRange,
    AboveRange,
    NotEvaluated,
}

public sealed record BaselineComparison(
    string SignalId,
    string ConditionId,
    double ObservedValue,
    string Unit,
    BaselineComparisonStatus Status,
    double? VarianceFromNearestBoundary,
    double? VariancePercent,
    string Explanation);
