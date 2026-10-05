using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MadlanExplorer.Tests;

public class TestHostEnvironment : IHostEnvironment
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
