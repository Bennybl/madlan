namespace MadlanExplorer;

public class DatasetMetadata
{
    public string FileHash { get; init; } = string.Empty;

    public int ReportCount { get; init; }
}

public class NormalizedDealReport
{
    public string DealId { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? Neighborhood { get; init; }

    public string? Street { get; init; }

    public string PropertyType { get; init; } = string.Empty;

    public decimal? Rooms { get; init; }

    public decimal? SizeSqm { get; init; }

    public decimal? Floor { get; init; }

    public decimal? TotalFloors { get; init; }

    public decimal? YearBuilt { get; init; }

    public string? Condition { get; init; }

    public decimal? PriceNis { get; init; }

    public decimal? SuppliedPricePerSqm { get; init; }

    public bool? HasElevator { get; init; }

    public bool? HasParking { get; init; }

    public bool? HasBalcony { get; init; }

    public bool? HasSafeRoom { get; init; }

    public DateOnly? DealDateStart { get; init; }

    public DateOnly? DealDateEnd { get; init; }

    public string DealDatePrecision { get; init; } = "unknown";

    public string Source { get; init; } = string.Empty;
}
