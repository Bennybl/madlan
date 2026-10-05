using System.Text.Json;

namespace MadlanExplorer;

public class DealQueryService
{
    private readonly DatasetStore _datasetStore;

    public DealQueryService(DatasetStore datasetStore)
    {
        _datasetStore = datasetStore;
    }

    public DealQueryResult Query(DealFilters filters)
    {
        Validate(filters);
        var deals = LoadUsableDeals();
        var exclusions = new Dictionary<string, int>(StringComparer.Ordinal);
        var matches = deals.Where(deal => Matches(deal, filters, exclusions)).ToList();
        var positivePrices = matches.Where(deal => deal.PriceNis is > 0).ToList();
        AddExclusion(exclusions, "non_positive_price", matches.Count - positivePrices.Count);
        var ratios = positivePrices.Where(deal => deal.SizeSqm is > 0).Select(deal => deal.PriceNis!.Value / deal.SizeSqm!.Value).ToList();
        AddExclusion(exclusions, "missing_or_non_positive_area", positivePrices.Count - ratios.Count);

        var warnings = new List<string>();
        if (positivePrices.Any(deal => deal.PriceNis < 100_000)) warnings.Add("low_price_reported");
        if (positivePrices.Count < 5) warnings.Add("price_metric_has_fewer_than_five_contributors");
        if (ratios.Count < 5) warnings.Add("price_per_sqm_metric_has_fewer_than_five_contributors");
        if (positivePrices.Any(deal => HasSuppliedPricePerSqmMismatch(deal))) warnings.Add("supplied_price_per_sqm_mismatch");

        return new DealQueryResult
        {
            TransactionCount = matches.Count,
            MedianPriceNis = Median(positivePrices.Select(deal => deal.PriceNis!.Value)),
            MedianPricePerSqm = Median(ratios),
            PriceContributorCount = positivePrices.Count,
            PricePerSqmContributorCount = ratios.Count,
            ContributorDealIds = matches.Select(deal => deal.DealId).Order().ToList(),
            PriceContributorDealIds = positivePrices.Select(deal => deal.DealId).Order().ToList(),
            PricePerSqmContributorDealIds = positivePrices.Where(deal => deal.SizeSqm is > 0).Select(deal => deal.DealId).Order().ToList(),
            ExclusionReasons = exclusions,
            Warnings = warnings
        };
    }

    private List<NormalizedDealReport> LoadUsableDeals()
    {
        using var connection = _datasetStore.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Reports.NormalizedJson
            FROM Deals
            INNER JOIN Reports ON Reports.Id = Deals.CanonicalReportId
            WHERE Deals.ConflictStatus = $status;
            """;
        command.Parameters.AddWithValue("$status", "usable");
        using var reader = command.ExecuteReader();
        var deals = new List<NormalizedDealReport>();
        while (reader.Read()) deals.Add(JsonSerializer.Deserialize<NormalizedDealReport>(reader.GetString(0))!);
        return deals;
    }

    private static bool Matches(NormalizedDealReport deal, DealFilters filters, IDictionary<string, int> exclusions)
    {
        if (!MatchesText(deal.City, filters.City) || !MatchesText(deal.Neighborhood, filters.Neighborhood) || !MatchesText(deal.PropertyType, filters.PropertyType))
        {
            AddExclusion(exclusions, "filter_mismatch", 1); return false;
        }
        if ((filters.MinimumRooms.HasValue || filters.MaximumRooms.HasValue) && (!deal.Rooms.HasValue || deal.Rooms < filters.MinimumRooms || deal.Rooms > filters.MaximumRooms))
        {
            AddExclusion(exclusions, "rooms_not_eligible", 1); return false;
        }
        if ((filters.StartDate.HasValue || filters.EndDate.HasValue) && (!deal.DealDateStart.HasValue || !deal.DealDateEnd.HasValue || deal.DealDateStart < filters.StartDate || deal.DealDateEnd > filters.EndDate))
        {
            AddExclusion(exclusions, "date_not_fully_contained", 1); return false;
        }
        return true;
    }

    private static bool MatchesText(string? value, string? filter) => string.IsNullOrWhiteSpace(filter) || string.Equals(value, filter.Trim(), StringComparison.OrdinalIgnoreCase);
    private static void AddExclusion(IDictionary<string, int> exclusions, string reason, int count) { if (count > 0) exclusions[reason] = exclusions.TryGetValue(reason, out var current) ? current + count : count; }
    private static decimal? Median(IEnumerable<decimal> values) { var ordered = values.Order().ToList(); return ordered.Count == 0 ? null : ordered.Count % 2 == 1 ? ordered[ordered.Count / 2] : (ordered[(ordered.Count / 2) - 1] + ordered[ordered.Count / 2]) / 2; }
    private static bool HasSuppliedPricePerSqmMismatch(NormalizedDealReport deal)
    {
        if (deal.PriceNis is not > 0 || deal.SizeSqm is not > 0 || !deal.SuppliedPricePerSqm.HasValue) return false;
        var calculated = deal.PriceNis.Value / deal.SizeSqm.Value;
        return Math.Abs(deal.SuppliedPricePerSqm.Value - calculated) > Math.Max(1m, calculated * .01m);
    }
    private static void Validate(DealFilters filters)
    {
        if (filters.MinimumRooms is < 0 || filters.MaximumRooms is < 0 || filters.MinimumRooms > filters.MaximumRooms) throw new ArgumentException("Room bounds are invalid.", nameof(filters));
        if (filters.StartDate > filters.EndDate) throw new ArgumentException("Date bounds are invalid.", nameof(filters));
    }
}
