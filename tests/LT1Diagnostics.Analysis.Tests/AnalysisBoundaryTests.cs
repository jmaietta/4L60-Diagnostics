namespace LT1Diagnostics.Analysis.Tests;

public sealed class AnalysisBoundaryTests
{
    [Fact]
    public void AnalysisAssemblyDoesNotReferenceUiOrTransportAssemblies()
    {
        string[] references = typeof(LT1Diagnostics.Analysis.AnalysisAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("Avalonia", references);
        Assert.DoesNotContain("System.IO.Ports", references);
    }
}
