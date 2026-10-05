namespace MadlanExplorer;

public class DealQueryResult
{
    public int TransactionCount { get; init; }
    public decimal? MedianPriceNis { get; init; }
    public decimal? MedianPricePerSqm { get; init; }
    public int PriceContributorCount { get; init; }
    public int PricePerSqmContributorCount { get; init; }
    public IReadOnlyList<string> ContributorDealIds { get; init; } = [];
    public IReadOnlyList<string> PriceContributorDealIds { get; init; } = [];
    public IReadOnlyList<string> PricePerSqmContributorDealIds { get; init; } = [];
    public IReadOnlyDictionary<string, int> ExclusionReasons { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
