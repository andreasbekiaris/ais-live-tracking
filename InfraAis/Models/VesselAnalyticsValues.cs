namespace InfraAis.Models;



public class VesselAnalyticsValues
{
    public long Mmsi { get; set; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}