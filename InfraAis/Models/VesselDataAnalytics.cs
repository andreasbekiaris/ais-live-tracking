namespace InfraAis.Models;

public class VesselDataAnalytics
{
    public long Mmsi { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal? Sog { get; set; }
    public int? NavStatus { get; set; }
    public DateTimeOffset MsgTimestampUtc { get; set; }
}