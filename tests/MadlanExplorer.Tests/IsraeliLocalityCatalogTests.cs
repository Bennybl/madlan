using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class IsraeliLocalityCatalogTests
{
    [Fact]
    public void Load_makes_the_full_locality_list_available_by_code_and_name()
    {
        var catalog = TestLocalityCatalog.CreateLoaded();

        Assert.Equal(1_316, catalog.Localities.Count);
        Assert.Equal("אבו גוש", catalog.FindByCode(472)?.NameHe);
        Assert.Equal("אבו גוש", catalog.FindCanonicalHebrewName("ABU GHOSH"));
    }

    [Fact]
    public void Resolve_corrects_only_clear_catalog_typos_and_preserves_unknown_values()
    {
        var catalog = TestLocalityCatalog.CreateLoaded();

        var corrected = catalog.Resolve("ABU GHOS");
        var unknown = catalog.Resolve("not-a-locality");

        Assert.Equal("typo", corrected.Method);
        Assert.Equal(472, corrected.OfficialCode);
        Assert.Equal("אבו גוש", corrected.ResolvedValue);
        Assert.Equal("unresolved", unknown.Method);
        Assert.Equal("not-a-locality", unknown.OriginalValue);
        Assert.False(unknown.OfficialCode.HasValue);
    }
}
