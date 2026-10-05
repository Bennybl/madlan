using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeDatasetMetadataProvider : IDatasetMetadataProvider
{
    public DatasetMetadata Metadata { get; set; } = new() { FileHash = "test-dataset-hash" };
}
