namespace MadlanExplorer;

public class QueryService
{
    private readonly IDealRepository _dealRepository;

    public QueryService(IDealRepository dealRepository)
    {
        _dealRepository = dealRepository;
    }

    public DealQueryResult Query(DealFilters filters)
    {
        ValidateFilters(filters);
        return _dealRepository.Execute(new DealQuery { Filters = filters });
    }

    public void Validate(DealFilters filters) => ValidateFilters(filters);

    public DataQueryResult ExecuteDataQuery(DataQuery query)
    {
        ValidateFilters(query.Filters);

        if (query.Aggregate != DataAggregate.Count && query.Field is null)
        {
            throw new ArgumentException("Field is required for this aggregate.", nameof(query));
        }

        if (query.Rank is < 1 or > 1000)
        {
            throw new ArgumentException("Rank must be between 1 and 1000.", nameof(query));
        }

        if (query.Limit is < 1 or > 200)
        {
            throw new ArgumentException("Limit must be between 1 and 200.", nameof(query));
        }

        return _dealRepository.ExecuteDataQuery(query);
    }

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
