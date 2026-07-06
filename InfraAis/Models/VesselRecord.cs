namespace InfraAis.Models;

public class VesselRecord
{
    public long Mmsi { get; set; }
    public int? Imo { get; set; }
    public string? Name { get; set; }
    public string? CallSign { get; set; }
    public int? ShipType { get; set; }
    public DateTime TimestampUtc { get; set; }
}