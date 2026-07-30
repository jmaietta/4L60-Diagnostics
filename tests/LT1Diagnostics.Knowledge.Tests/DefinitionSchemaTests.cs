using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace LT1Diagnostics.Knowledge.Tests;

public sealed class DefinitionSchemaTests
{
    public static TheoryData<string, string> DefinitionFamilies => new()
    {
        { "vehicles", "vehicle.schema.json" },
        { "signals", "signal.schema.json" },
        { "dtcs", "dtc.schema.json" },
        { "tests", "test.schema.json" },
        { "baselines", "baseline.schema.json" },
        { "commentary", "commentary.schema.json" },
        { "protocol", "protocol.schema.json" },
        { "dtc-catalogs", "dtc-catalog.schema.json" },
    };

    [Theory]
    [MemberData(nameof(DefinitionFamilies))]
    public async Task EveryDefinitionValidatesAgainstItsSchema(string family, string schemaFile)
    {
        string root = FindRepositoryRoot();
        string schemaText = await File.ReadAllTextAsync(Path.Combine(root, "definitions", "schemas", schemaFile));
        JsonSchema schema = JsonSchema.FromText(schemaText);

        string[] definitions = Directory.GetFiles(Path.Combine(root, "definitions", family), "*.json");
        Assert.NotEmpty(definitions);

        foreach (string definitionPath in definitions)
        {
            using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(definitionPath));
            EvaluationResults result = schema.Evaluate(instance.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

            Assert.True(result.IsValid, $"{definitionPath} failed schema validation: {result}");
        }
    }

    [Theory]
    [MemberData(nameof(DefinitionFamilies))]
    public async Task EveryPlaceholderIsExplicitlyIneligible(string family, string schemaFile)
    {
        _ = schemaFile;
        string root = FindRepositoryRoot();
        foreach (string path in Directory.GetFiles(Path.Combine(root, "definitions", family), "*.json"))
        {
            JsonNode document = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            Assert.Equal("Unverified", document["verificationStatus"]?.GetValue<string>());
            Assert.False(document["productionEligible"]?.GetValue<bool>());
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
