using System.Xml.Linq;

namespace LT1Diagnostics.Domain.Tests;

public sealed class DependencyBoundaryTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["LT1Diagnostics.Domain"] = [],
            ["LT1Diagnostics.Protocol"] = ["LT1Diagnostics.Domain"],
            ["LT1Diagnostics.Transport"] = ["LT1Diagnostics.Domain"],
            ["LT1Diagnostics.Acquisition"] = ["LT1Diagnostics.Domain", "LT1Diagnostics.Protocol", "LT1Diagnostics.Transport"],
            ["LT1Diagnostics.Analysis"] = ["LT1Diagnostics.Domain"],
            ["LT1Diagnostics.Knowledge"] = ["LT1Diagnostics.Domain"],
            ["LT1Diagnostics.Reporting"] = ["LT1Diagnostics.Analysis", "LT1Diagnostics.Domain", "LT1Diagnostics.Knowledge"],
            ["LT1Diagnostics.Simulator"] = ["LT1Diagnostics.Domain", "LT1Diagnostics.Protocol", "LT1Diagnostics.Transport"],
        };

    [Fact]
    public void ProductionProjectReferencesMatchTheApprovedGraph()
    {
        string root = FindRepositoryRoot();
        foreach ((string projectName, string[] expected) in ExpectedReferences)
        {
            string path = Path.Combine(root, "src", projectName, $"{projectName}.csproj");
            XDocument document = XDocument.Load(path);
            string[] actual = document.Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include"))
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => Path.GetFileNameWithoutExtension(reference!.Replace('\\', '/')))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BUILD_PLAN.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
