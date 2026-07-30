using LT1Diagnostics.Domain.Diagnostics;

namespace LT1Diagnostics.Reporting;

public sealed record DiagnosticReportDtc(
    int Code,
    string Title,
    string Meaning,
    string PossibleCauses,
    string NextCheck,
    string EvidenceStatus);

public sealed record DiagnosticReportInput(
    string Vehicle,
    DateTimeOffset GeneratedAt,
    string SessionFileName,
    string EvidenceLabel,
    string DataQuality,
    TransmissionSessionAnalysis Analysis,
    IReadOnlyList<TransmissionObservation> Observations,
    IReadOnlyList<DiagnosticReportDtc> TroubleCodes);
