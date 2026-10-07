namespace MadlanExplorer;

public class LocalityResolution
{
    public const string RuleVersion = "catalog-v1";

    public int? OfficialCode { get; init; }

    public string OriginalValue { get; init; } = string.Empty;

    public string ResolvedValue { get; init; } = string.Empty;

    public string Method { get; init; } = "unresolved";

    public string Rule { get; init; } = RuleVersion;
}
