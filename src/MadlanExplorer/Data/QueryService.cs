namespace MadlanExplorer;

public class QueryService
{
    private readonly IDealRepository _dealRepository;

    public QueryService(IDealRepository dealRepository)
    {
        _dealRepository = dealRepository;
    }

    public DealQueryResult Query(
        DealFilters filters,
        IReadOnlyList<QueryMetric>? metrics = null,
        IReadOnlyList<RankedMetricRequest>? rankedMetrics = null,
        GroupByField? groupBy = null,
        IReadOnlyList<OutlierField>? outlierFields = null)
    {
        ValidateFilters(filters);
        var ranked = rankedMetrics ?? [];
        ValidateRankedMetrics(ranked);
        return _dealRepository.Execute(new DealQuery
        {
            Filters = filters,
            Metrics = metrics ?? [],
            RankedMetrics = ranked,
            GroupBy = groupBy,
            OutlierFields = outlierFields ?? []
        });
    }

    public void Validate(DealFilters filters) => ValidateFilters(filters);

    private static void ValidateRankedMetrics(IReadOnlyList<RankedMetricRequest> rankedMetrics)
    {
        foreach (var request in rankedMetrics)
        {
            if (!QueryMetrics.Rankable.Contains(request.Metric))
            {
                throw new ArgumentException($"{request.Metric} cannot be ranked; only Min/Max metrics support a rank.", nameof(rankedMetrics));
            }

            if (request.Rank is < 1 or > 1000)
            {
                throw new ArgumentException("Rank must be between 1 and 1000.", nameof(rankedMetrics));
            }
        }
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
