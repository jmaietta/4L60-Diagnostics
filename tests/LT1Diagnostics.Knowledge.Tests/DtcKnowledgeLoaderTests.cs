using System.Text;
using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Knowledge.Dtcs;

namespace LT1Diagnostics.Knowledge.Tests;

public sealed class DtcKnowledgeLoaderTests
{
    [Fact]
    public async Task LoadsDocumentaryTransmissionCatalogWithRankedCausesAndNextTests()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "definitions", "dtcs");

        DtcKnowledgeCatalog catalog = await DtcKnowledgeLoader.LoadDirectoryAsync(directory);

        Assert.Equal(13, catalog.Count);
        Assert.Contains(73, catalog.Codes);
        Assert.True(catalog.TryGet(73, out DtcKnowledgeDefinition? definition));
        Assert.NotNull(definition);
        Assert.Equal(VerificationStatus.Unverified, definition.VerificationStatus);
        Assert.False(definition.ProductionEligible);
        Assert.NotEmpty(definition.LikelyCauses);
        Assert.NotEmpty(definition.ConfirmatoryTests);
    }

    [Fact]
    public async Task RejectsUnverifiedDefinitionMarkedProductionEligible()
    {
        const string json = """
            {
              "schemaVersion": "1.0.0",
              "definitionVersion": "test",
              "verificationStatus": "Unverified",
              "productionEligible": true,
              "definitionId": "unsafe",
              "code": "73",
              "codeFormat": "GM OBD-I two-digit",
              "system": "Transmission",
              "title": "Unsafe",
              "plainEnglishMeaning": null,
              "enableCriteria": null,
              "failureCriteria": null,
              "maturityCriteria": null,
              "pcmFallbackAction": [],
              "driverSymptoms": [],
              "likelyCauses": [],
              "falsePositiveConditions": [],
              "confirmatoryTests": [],
              "safetyLevel": null,
              "sourceReferences": []
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<InvalidDataException>(() => DtcKnowledgeLoader.LoadAsync(stream));
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
