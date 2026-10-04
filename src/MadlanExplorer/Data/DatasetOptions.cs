namespace MadlanExplorer;

public class DatasetOptions
{
    public const string SectionName = "Dataset";

    public string DataFile { get; set; } = "data/madlan_deals_sample.csv";

    public string LocalitiesFile { get; set; } = "Data/israeli-localities.json";

    public string DatabaseName { get; set; } = "Madlan";
}
