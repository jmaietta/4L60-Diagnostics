using System.Text.Json;
using LT1Diagnostics.Protocol.A276;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class A276DtcCatalogConsistencyTests
{
    [Fact]
    public async Task CompiledDtcMapMatchesVersionedDocumentaryCatalog()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(
            root,
            "definitions",
            "dtc-catalogs",
            "a276-mode1-message1-transmission.unverified.json");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var catalogEntries = document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(element => new
            {
                Code = element.GetProperty("code").GetInt32(),
                Title = element.GetProperty("sourceTitle").GetString(),
                Offset = element.GetProperty("dataByteOffset").GetInt32(),
                Bit = element.GetProperty("bit").GetInt32(),
            })
            .OrderBy(entry => entry.Code)
            .ToArray();
        var compiledEntries = A276TransmissionDecoder.LoggedTransmissionDtcDefinitions
            .Select(entry => new
            {
                entry.Code,
                Title = (string?)entry.SourceTitle,
                Offset = entry.DataByteOffset,
                entry.Bit,
            })
            .OrderBy(entry => entry.Code)
            .ToArray();

        Assert.Equal(catalogEntries, compiledEntries);
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
