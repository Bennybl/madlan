using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class IsraeliLocalityCatalogTests
{
    [Fact]
    public void Load_makes_the_full_locality_list_available_by_code_and_name()
    {
        var environment = new TestHostEnvironment(AppContext.BaseDirectory);
        var options = Options.Create(new DatasetOptions
        {
            LocalitiesFile = "Data/israeli-localities.json"
        });
        var catalog = new IsraeliLocalityCatalog(options, environment);

        catalog.Load();

        Assert.Equal(1_316, catalog.Localities.Count);
        Assert.Equal("אבו גוש", catalog.FindByCode(472)?.NameHe);
        Assert.Equal("אבו גוש", catalog.FindCanonicalHebrewName("ABU GHOSH"));
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "MadlanExplorer.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
