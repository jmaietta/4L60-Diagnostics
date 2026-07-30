using System.Text.Json;
using System.Text.Json.Serialization;
using LT1Diagnostics.Domain.Definitions;

namespace LT1Diagnostics.Knowledge.Dtcs;

public static class DtcKnowledgeLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<DtcKnowledgeDefinition> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        DtcKnowledgeDefinition definition = await JsonSerializer
            .DeserializeAsync<DtcKnowledgeDefinition>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The DTC definition is empty.");
        Validate(definition);
        return definition;
    }

    public static async Task<DtcKnowledgeCatalog> LoadDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return DtcKnowledgeCatalog.Empty;
        }

        var definitions = new List<DtcKnowledgeDefinition>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").Order())
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            definitions.Add(await LoadAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        return new DtcKnowledgeCatalog(definitions);
    }

    private static void Validate(DtcKnowledgeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.DefinitionId) || string.IsNullOrWhiteSpace(definition.Title))
        {
            throw new InvalidDataException("A DTC definition requires an ID and title.");
        }

        if (definition.VerificationStatus == VerificationStatus.Unverified && definition.ProductionEligible)
        {
            throw new InvalidDataException("An unverified DTC definition cannot be production eligible.");
        }

        if (definition.LikelyCauses.Any(cause =>
            cause.Rank <= 0 ||
            string.IsNullOrWhiteSpace(cause.Category) ||
            string.IsNullOrWhiteSpace(cause.Cause)))
        {
            throw new InvalidDataException("Every ranked DTC cause requires a positive rank, category, and description.");
        }

        if (definition.LikelyCauses.Select(cause => cause.Rank).Distinct().Count() != definition.LikelyCauses.Count)
        {
            throw new InvalidDataException("DTC cause ranks must be unique within a definition.");
        }
    }
}
