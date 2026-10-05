using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class QueryServiceTests
{
    [Fact]
    public void Query_sends_a_validated_provider_neutral_query_to_the_repository()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);
        var filters = new DealFilters { City = "חולון", MinimumRooms = 4, MaximumRooms = 4 };

        var result = service.Query(filters);

        Assert.Equal(7, result.TransactionCount);
        Assert.Same(filters, repository.ReceivedQuery?.Filters);
        Assert.Equal(100, repository.ReceivedQuery?.EvidencePageSize);
    }

    [Fact]
    public void Query_rejects_invalid_filters_before_calling_the_repository()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumRooms = 5, MaximumRooms = 4 }));
        Assert.Null(repository.ReceivedQuery);
    }

    [Fact]
    public void Query_defaults_to_no_requested_metrics_when_none_are_specified()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        service.Query(new DealFilters());

        Assert.Empty(repository.ReceivedQuery?.Metrics ?? []);
    }

    [Fact]
    public void Query_passes_the_requested_metrics_to_the_repository()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        service.Query(new DealFilters(), [QueryMetric.MaxPrice, QueryMetric.MedianPrice]);

        Assert.Equal([QueryMetric.MaxPrice, QueryMetric.MedianPrice], repository.ReceivedQuery?.Metrics);
    }

    [Fact]
    public void Query_rejects_invalid_floor_and_year_built_bounds()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumFloor = 5, MaximumFloor = 2 }));
        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumYearBuilt = 2020, MaximumYearBuilt = 2000 }));
        Assert.Null(repository.ReceivedQuery);
    }
}
