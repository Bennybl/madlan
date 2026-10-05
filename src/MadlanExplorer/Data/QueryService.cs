namespace MadlanExplorer;

public class QueryService
{
    private readonly IDealRepository _dealRepository;

    public QueryService(IDealRepository dealRepository)
    {
        _dealRepository = dealRepository;
    }

    public DealQueryResult Query(DealFilters filters, IReadOnlyList<QueryMetric>? metrics = null)
    {
        ValidateFilters(filters);
        return _dealRepository.Execute(new DealQuery { Filters = filters, Metrics = metrics ?? [] });
    }

    public void Validate(DealFilters filters) => ValidateFilters(filters);

    private static void ValidateFilters(DealFilters filters)
    {
        if (filters.MinimumRooms is < 0 || filters.MaximumRooms is < 0 || filters.MinimumRooms > filters.MaximumRooms)
        {
            throw new ArgumentException("Room bounds are invalid.", nameof(filters));
        }

        if (filters.StartDate > filters.EndDate)
        {
            throw new ArgumentException("Date bounds are invalid.", nameof(filters));
        }

        if (filters.MinimumFloor > filters.MaximumFloor)
        {
            throw new ArgumentException("Floor bounds are invalid.", nameof(filters));
        }

        if (filters.MinimumYearBuilt > filters.MaximumYearBuilt)
        {
            throw new ArgumentException("Year-built bounds are invalid.", nameof(filters));
        }
    }
}
