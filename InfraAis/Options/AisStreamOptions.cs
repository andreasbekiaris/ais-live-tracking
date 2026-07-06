
namespace InfraAis.Options;

public class AisStreamOptions
{
    public const string SectionName = "AisStream";

    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public BoundingBoxOptions BoundingBox { get; set; } = new();
    public string[] FilterMessageTypes { get; set; } = [];
}

public class BoundingBoxOptions
{
    public double MinLat { get; set; }
    public double MinLon { get; set; }
    public double MaxLat { get; set; }
    public double MaxLon { get; set; }
}