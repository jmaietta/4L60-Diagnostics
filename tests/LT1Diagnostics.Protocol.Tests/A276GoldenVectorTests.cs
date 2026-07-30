using System.Text.Json;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class A276GoldenVectorTests
{
    [Fact]
    public async Task EveryDocumentaryRequestVectorMatchesProductionBuilder()
    {
        string root = FindRepositoryRoot();
        string json = await File.ReadAllTextAsync(Path.Combine(root, "testdata", "golden", "a276-documentary-vectors.json"));
        using JsonDocument document = JsonDocument.Parse(json);

        foreach (JsonElement vector in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            byte[] expected = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
            string kind = vector.GetProperty("kind").GetString()!;
            byte[] actual = kind switch
            {
                "Mode1" => A276MessageFactory.CreateMode1Request(vector.GetProperty("datasetId").GetByte()),
                "Control" => BuildControl(vector.GetProperty("mode").GetByte()),
                _ => throw new InvalidDataException($"Unsupported golden-vector kind: {kind}."),
            };

            Assert.Equal(expected, actual);
            Assert.True(AldlChecksum.IsValid(actual));
        }
    }

    private static byte[] BuildControl(byte mode) => mode switch
    {
        0 => A276MessageFactory.CreateReturnToNormalModeRequest(),
        8 => A276MessageFactory.CreateDisableNormalCommunicationsRequest(),
        9 => A276MessageFactory.CreateEnableNormalCommunicationsRequest(),
        _ => throw new InvalidDataException($"Unsupported control mode: {mode}."),
    };

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
