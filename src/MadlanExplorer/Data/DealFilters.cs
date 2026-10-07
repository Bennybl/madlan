namespace MadlanExplorer;

public class DealFilters
{
    public string? City { get; init; }

    public string? Neighborhood { get; init; }

    public string? PropertyType { get; init; }

    public decimal? MinimumRooms { get; init; }

    public decimal? MaximumRooms { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public decimal? MinimumFloor { get; init; }

    public decimal? MaximumFloor { get; init; }

    public decimal? MinimumYearBuilt { get; init; }

    public decimal? MaximumYearBuilt { get; init; }

    public string? Condition { get; init; }

    public string? Source { get; init; }

    public bool? HasElevator { get; init; }

    public bool? HasParking { get; init; }

    public bool? HasBalcony { get; init; }

    public bool? HasSafeRoom { get; init; }
}
