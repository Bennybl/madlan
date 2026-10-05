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
}
