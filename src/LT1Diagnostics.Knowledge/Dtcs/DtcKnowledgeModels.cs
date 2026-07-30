using LT1Diagnostics.Domain.Definitions;

namespace LT1Diagnostics.Knowledge.Dtcs;

public sealed record DtcCauseDefinition(
    int Rank,
    string Category,
    string Cause);

public sealed record DtcKnowledgeDefinition(
    string SchemaVersion,
    string DefinitionVersion,
    VerificationStatus VerificationStatus,
    bool ProductionEligible,
    string DefinitionId,
    string? Code,
    string? CodeFormat,
    string? System,
    string Title,
    string? PlainEnglishMeaning,
    string? EnableCriteria,
    string? FailureCriteria,
    string? MaturityCriteria,
    IReadOnlyList<string> PcmFallbackAction,
    IReadOnlyList<string> DriverSymptoms,
    IReadOnlyList<DtcCauseDefinition> LikelyCauses,
    IReadOnlyList<string> FalsePositiveConditions,
    IReadOnlyList<string> ConfirmatoryTests,
    string? SafetyLevel,
    IReadOnlyList<string> SourceReferences);

public sealed class DtcKnowledgeCatalog
{
    private readonly IReadOnlyDictionary<int, DtcKnowledgeDefinition> _definitions;

    public DtcKnowledgeCatalog(IEnumerable<DtcKnowledgeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = definitions
            .Where(definition => int.TryParse(definition.Code, out _))
            .ToDictionary(
                definition => int.Parse(definition.Code!, System.Globalization.CultureInfo.InvariantCulture),
                definition => definition);
    }

    public int Count => _definitions.Count;

    public IReadOnlyCollection<int> Codes => _definitions.Keys.Order().ToArray();

    public bool TryGet(int code, out DtcKnowledgeDefinition? definition) =>
        _definitions.TryGetValue(code, out definition);

    public static DtcKnowledgeCatalog Empty { get; } = new([]);
}
