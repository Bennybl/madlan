using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class DatasetQueryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DatasetQueryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Query_returns_the_documented_holon_four_room_deal()
    {
        var result = Query(new DealFilters
        {
            City = "חולון",
            PropertyType = "דירה",
            MinimumRooms = 4,
            MaximumRooms = 4,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31)
        });

        Assert.Equal(1, result.TransactionCount);
        Assert.Equal(3_826_000m, result.MedianPriceNis);
        Assert.Equal(38_260m, result.MedianPricePerSqm);
        Assert.Equal(["D100027"], result.ContributorDealIds);
        Assert.Equal(["D100027"], result.PriceContributorDealIds);
        Assert.Equal(["D100027"], result.PricePerSqmContributorDealIds);
    }

    [Fact]
    public void Query_excludes_partial_month_dates()
    {
        var result = Query(new DealFilters
        {
            StartDate = new DateOnly(2025, 8, 2),
            EndDate = new DateOnly(2025, 8, 31)
        });

        Assert.DoesNotContain("D100171", result.ContributorDealIds);
    }

    [Fact]
    public void Query_returns_null_metrics_when_no_deals_match()
    {
        var result = Query(new DealFilters { City = "לא קיימת" });

        Assert.Equal(0, result.TransactionCount);
        Assert.Null(result.MedianPriceNis);
        Assert.Null(result.MedianPricePerSqm);
    }

    [Fact]
    public void Query_rejects_invalid_filter_ranges()
    {
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { MinimumRooms = 5, MaximumRooms = 4 }));
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { StartDate = new DateOnly(2025, 2, 1), EndDate = new DateOnly(2025, 1, 1) }));
    }

    private DealQueryResult Query(DealFilters filters)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DatasetStore>().ExecuteQuery(filters);
    }
}
