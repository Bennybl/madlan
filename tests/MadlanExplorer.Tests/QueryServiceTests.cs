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
    public void Query_rejects_invalid_floor_and_year_built_bounds()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumFloor = 5, MaximumFloor = 2 }));
        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumYearBuilt = 2020, MaximumYearBuilt = 2000 }));
        Assert.Null(repository.ReceivedQuery);
    }

    [Fact]
    public void ExecuteDataQuery_sends_a_validated_query_to_the_repository()
    {
        var repository = new FakeDealRepository { DataResult = new DataQueryResult { TransactionCount = 5 } };
        var service = new QueryService(repository);
        var query = new DataQuery { Filters = new DealFilters { City = "חולון" }, Aggregate = DataAggregate.Count };

        var result = service.ExecuteDataQuery(query);

        Assert.Equal(5, result.TransactionCount);
        Assert.Same(query, Assert.Single(repository.ReceivedDataQueries));
    }

    [Fact]
    public void ExecuteDataQuery_rejects_invalid_filters_before_calling_the_repository()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.ExecuteDataQuery(new DataQuery { Filters = new DealFilters { MinimumRooms = 5, MaximumRooms = 4 } }));
        Assert.Empty(repository.ReceivedDataQueries);
    }

    [Theory]
    [InlineData(DataAggregate.Average)]
    [InlineData(DataAggregate.Median)]
    [InlineData(DataAggregate.Min)]
    [InlineData(DataAggregate.Max)]
    [InlineData(DataAggregate.Outliers)]
    public void ExecuteDataQuery_requires_a_field_for_every_aggregate_except_count(DataAggregate aggregate)
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.ExecuteDataQuery(new DataQuery { Aggregate = aggregate, Field = null }));
        Assert.Empty(repository.ReceivedDataQueries);
    }

    [Fact]
    public void ExecuteDataQuery_does_not_require_a_field_for_count()
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        service.ExecuteDataQuery(new DataQuery { Aggregate = DataAggregate.Count, Field = null });

        Assert.Single(repository.ReceivedDataQueries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void ExecuteDataQuery_rejects_an_out_of_range_rank(int rank)
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.ExecuteDataQuery(new DataQuery { Aggregate = DataAggregate.Max, Field = DataField.Price, Rank = rank }));
        Assert.Empty(repository.ReceivedDataQueries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void ExecuteDataQuery_rejects_an_out_of_range_limit(int limit)
    {
        var repository = new FakeDealRepository();
        var service = new QueryService(repository);

        Assert.Throws<ArgumentException>(() => service.ExecuteDataQuery(new DataQuery { Aggregate = DataAggregate.Count, Limit = limit }));
        Assert.Empty(repository.ReceivedDataQueries);
    }
}
