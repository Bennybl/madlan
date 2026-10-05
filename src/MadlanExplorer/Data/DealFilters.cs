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
}
