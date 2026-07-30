using LT1Diagnostics.Analysis;
using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Domain.Diagnostics;
using LT1Diagnostics.Reporting;

namespace LT1Diagnostics.App.Tests;

public sealed class DiagnosticReportGeneratorTests
{
    [Fact]
    public void HtmlReportEscapesUserFacingValuesAndKeepsEvidenceBoundary()
    {
        TransmissionObservation[] observations = [CreateObservation()];
        var input = new DiagnosticReportInput(
            "Roadmaster <test>",
            DateTimeOffset.UnixEpoch,
            "capture.lt1raw",
            "VEHICLE VALIDATION PENDING",
            "CLEAN",
            TransmissionSessionAnalyzer.Analyze(observations),
            observations,
            []);

        string result = DiagnosticReportGenerator.GenerateHtml(input);

        Assert.Contains("Roadmaster &lt;test&gt;", result, StringComparison.Ordinal);
        Assert.Contains("VEHICLE VALIDATION PENDING", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Roadmaster <test>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvUsesInvariantNumericFormatting()
    {
        string result = DiagnosticReportGenerator.GenerateCsv([CreateObservation()]);

        Assert.Contains("12.500,900.000,2,45.250", result, StringComparison.Ordinal);
        Assert.DoesNotContain("12,500", result, StringComparison.Ordinal);
    }

    private static TransmissionObservation CreateObservation() => new(
        TimeSpan.FromSeconds(1.25),
        900,
        12.5,
        2,
        45.25,
        70,
        13.4,
        80,
        1.5,
        1.4,
        false,
        false,
        true,
        false,
        VerificationStatus.Unverified);
}
