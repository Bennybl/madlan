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

    [Fact]
    public void DataQuery_computes_min_and_max_price_for_the_documented_holon_four_room_deal()
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

        var min = Single(ExecuteDataQuery(filters, DataAggregate.Min, DataField.Price));
        var max = Single(ExecuteDataQuery(filters, DataAggregate.Max, DataField.Price));

        Assert.Equal(3_826_000m, min.Value);
        Assert.Equal("D100027", min.DealId);
        Assert.Equal(3_826_000m, max.Value);
        Assert.Equal("D100027", max.DealId);
    }

    [Fact]
    public void DataQuery_computes_distinct_min_and_max_price_deals_for_a_broader_filter()
    {
        var filters = new DealFilters { City = "חולון" };

        var min = Single(ExecuteDataQuery(filters, DataAggregate.Min, DataField.Price));
        var max = Single(ExecuteDataQuery(filters, DataAggregate.Max, DataField.Price));
        var average = Single(ExecuteDataQuery(filters, DataAggregate.Average, DataField.Price));

        Assert.NotNull(min.Value);
        Assert.NotNull(max.Value);
        Assert.True(min.Value < max.Value);
        Assert.NotEqual(min.DealId, max.DealId);
        Assert.InRange(average.Value!.Value, min.Value.Value, max.Value.Value);
    }

    [Fact]
    public void DataQuery_computes_the_second_highest_price_via_rank()
    {
        var filters = new DealFilters { City = "חולון" };

        var first = Single(ExecuteDataQuery(filters, DataAggregate.Max, DataField.Price, rank: 1));
        var second = Single(ExecuteDataQuery(filters, DataAggregate.Max, DataField.Price, rank: 2));

        Assert.NotNull(second.Value);
        Assert.True(second.Value <= first.Value);
        Assert.NotEqual(first.DealId, second.DealId);
    }

    [Fact]
    public void DataQuery_returns_no_row_when_the_rank_exceeds_the_matching_deals()
    {
        var result = ExecuteDataQuery(new DealFilters { City = "חולון" }, DataAggregate.Max, DataField.Price, rank: 500);

        Assert.Empty(result.Rows);
    }

    [Fact]
    public void DataQuery_computes_the_cheapest_deal_per_city_when_grouped()
    {
        var result = ExecuteDataQuery(new DealFilters(), DataAggregate.Min, DataField.Price, groupBy: GroupByField.City);

        Assert.NotEmpty(result.Rows);
        var holon = Assert.Single(result.Rows, r => r.GroupValue == "חולון");
        Assert.NotNull(holon.Value);
        Assert.NotNull(holon.DealId);

        var ungroupedHolon = Single(ExecuteDataQuery(new DealFilters { City = "חולון" }, DataAggregate.Min, DataField.Price));
        Assert.Equal(ungroupedHolon.Value, holon.Value);
        Assert.Equal(ungroupedHolon.DealId, holon.DealId);
    }

    [Fact]
    public void DataQuery_top_n_groups_narrows_a_grouped_query_to_the_highest_valued_groups()
    {
        var all = ExecuteDataQuery(new DealFilters(), DataAggregate.Average, DataField.Price, groupBy: GroupByField.City);
        var top5 = ExecuteDataQuery(new DealFilters(), DataAggregate.Average, DataField.Price, groupBy: GroupByField.City, limit: 5, descending: true);

        Assert.Equal(5, top5.Rows.Count);
        var expectedTop5 = all.Rows.Where(r => r.Value is not null).OrderByDescending(r => r.Value).Take(5).Select(r => r.GroupValue).ToHashSet();
        Assert.Equal(expectedTop5, top5.Rows.Select(r => r.GroupValue).ToHashSet());
    }

    [Fact]
    public void DataQuery_group_by_respects_an_additional_filter()
    {
        var result = ExecuteDataQuery(new DealFilters { PropertyType = "דירה" }, DataAggregate.Average, DataField.Price, groupBy: GroupByField.City);

        Assert.NotEmpty(result.Rows);
    }

    [Fact]
    public void DataQuery_flags_values_outside_the_interquartile_fence_as_outliers()
    {
        var result = ExecuteDataQuery(new DealFilters(), DataAggregate.Outliers, DataField.Price);

        Assert.NotEmpty(result.Rows);
        foreach (var row in result.Rows)
        {
            Assert.True(row.Value < row.LowerBound || row.Value > row.UpperBound);
            Assert.True(row.LowerBound < row.UpperBound);
        }

        var dealIds = result.Rows.Select(r => r.DealId).ToList();
        Assert.Equal(dealIds.Distinct().Count(), dealIds.Count);
    }

    [Fact]
    public void DataQuery_computes_outliers_independently_per_group()
    {
        var result = ExecuteDataQuery(new DealFilters(), DataAggregate.Outliers, DataField.Price, groupBy: GroupByField.City);

        Assert.NotEmpty(result.Rows);
        foreach (var row in result.Rows)
        {
            Assert.NotNull(row.GroupValue);
        }
    }

    [Fact]
    public void DataQuery_groups_by_a_boolean_amenity_field()
    {
        var result = ExecuteDataQuery(new DealFilters(), DataAggregate.Count, groupBy: GroupByField.HasElevator);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.GroupValue == "1");
        Assert.Contains(result.Rows, r => r.GroupValue == "0");
    }

    private DealQueryResult Query(DealFilters filters)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<QueryService>().Query(filters);
    }

    private DataQueryResult ExecuteDataQuery(
        DealFilters filters,
        DataAggregate aggregate,
        DataField? field = null,
        GroupByField? groupBy = null,
        int rank = 1,
        int? limit = null,
        bool descending = true)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<QueryService>().ExecuteDataQuery(new DataQuery
        {
            Filters = filters,
            Aggregate = aggregate,
            Field = field,
            GroupBy = groupBy,
            Rank = rank,
            Limit = limit,
            Descending = descending
        });
    }

    private static DataRow Single(DataQueryResult result) => Assert.Single(result.Rows);
}
