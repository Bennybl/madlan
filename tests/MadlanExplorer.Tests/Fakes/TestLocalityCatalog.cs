using MadlanExplorer;
using Microsoft.Extensions.Options;

namespace MadlanExplorer.Tests;

public static class TestLocalityCatalog
{
    public static IsraeliLocalityCatalog CreateLoaded()
    {
        var environment = new TestHostEnvironment(AppContext.BaseDirectory);
        var options = Options.Create(new DatasetOptions { LocalitiesFile = "Data/israeli-localities.json" });
        var catalog = new IsraeliLocalityCatalog(options, environment);
        catalog.Load();
        return catalog;
    }
}
