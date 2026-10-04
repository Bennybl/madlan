namespace MadlanExplorer;

public class DatasetOptions
{
    public const string SectionName = "Dataset";

    public string DataFile { get; set; } = "data/madlan_deals_sample.csv";

    public string DatabaseName { get; set; } = "Madlan";
}
