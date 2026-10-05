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
        Assert.Contains("price_metric_has_fewer_than_five_contributors", result.Warnings);
        Assert.Contains("price_per_sqm_metric_has_fewer_than_five_contributors", result.Warnings);
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
    public void Query_limits_evidence_to_the_default_page_size()
    {
        var result = Query(new DealFilters());

        Assert.True(result.HasMoreEvidence);
        Assert.Equal(100, result.ContributorDealIds.Count);
        Assert.Equal(516, result.TransactionCount);
        Assert.DoesNotContain("D100017", result.ContributorDealIds);
    }

    [Fact]
    public void Query_rejects_invalid_filter_ranges()
    {
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { MinimumRooms = 5, MaximumRooms = 4 }));
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { StartDate = new DateOnly(2025, 2, 1), EndDate = new DateOnly(2025, 1, 1) }));
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { MinimumFloor = 5, MaximumFloor = 2 }));
        Assert.Throws<ArgumentException>(() => Query(new DealFilters { MinimumYearBuilt = 2020, MaximumYearBuilt = 2000 }));
    }

    [Fact]
    public void Query_computes_min_and_max_price_for_the_documented_holon_four_room_deal()
    {
        var filters = new DealFilters
        {
            City = "חולון",
            PropertyType = "דירה",
            MinimumRooms = 4,
            MaximumRooms = 4,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31)
        };

        var minResult = Query(filters, QueryMetric.MinPrice);
        var maxResult = Query(filters, QueryMetric.MaxPrice);

        Assert.Equal(QueryMetric.MinPrice, minResult.RequestedMetric);
        Assert.Equal(3_826_000m, minResult.RequestedMetricValue);
        Assert.Equal("D100027", minResult.RequestedMetricDealId);

        Assert.Equal(QueryMetric.MaxPrice, maxResult.RequestedMetric);
        Assert.Equal(3_826_000m, maxResult.RequestedMetricValue);
        Assert.Equal("D100027", maxResult.RequestedMetricDealId);
    }

    [Fact]
    public void Query_computes_distinct_min_and_max_price_deals_for_a_broader_filter()
    {
        var filters = new DealFilters { City = "חולון" };

        var countResult = Query(filters);
        var minResult = Query(filters, QueryMetric.MinPrice);
        var maxResult = Query(filters, QueryMetric.MaxPrice);
        var averageResult = Query(filters, QueryMetric.AveragePrice);

        Assert.True(countResult.TransactionCount > 1);
        Assert.NotNull(minResult.RequestedMetricValue);
        Assert.NotNull(maxResult.RequestedMetricValue);
        Assert.True(minResult.RequestedMetricValue < maxResult.RequestedMetricValue);
        Assert.NotEqual(minResult.RequestedMetricDealId, maxResult.RequestedMetricDealId);
        Assert.InRange(averageResult.RequestedMetricValue!.Value, minResult.RequestedMetricValue.Value, maxResult.RequestedMetricValue.Value);
    }

    [Fact]
    public void Query_does_not_compute_a_requested_metric_when_none_is_asked_for()
    {
        var result = Query(new DealFilters { City = "חולון" });

        Assert.Null(result.RequestedMetric);
        Assert.Null(result.RequestedMetricValue);
        Assert.Null(result.RequestedMetricDealId);
    }

    [Fact]
    public void Query_filters_by_boolean_amenity_columns()
    {
        var withElevator = Query(new DealFilters { HasElevator = true });
        var withoutElevator = Query(new DealFilters { HasElevator = false });
        var unfiltered = Query(new DealFilters());

        Assert.True(withElevator.TransactionCount > 0);
        Assert.True(withoutElevator.TransactionCount > 0);
        Assert.True(withElevator.TransactionCount < unfiltered.TransactionCount);
        Assert.True(withoutElevator.TransactionCount < unfiltered.TransactionCount);
    }

    private DealQueryResult Query(DealFilters filters, QueryMetric metric = QueryMetric.TransactionCount)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<QueryService>().Query(filters, metric);
    }
}
