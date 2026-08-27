namespace InfraAis.Models;

public class VesselRecord
{
    public long Mmsi { get; set; }
    public int? Imo { get; set; }
    public string? Name { get; set; }
    public string? CallSign { get; set; }
    public int? ShipType { get; set; }
    public DateTime TimestampUtc { get; set; }
    public short? DimToBow { get; set; }
    public short? DimToStern { get; set; }
    public short? DimToPort { get; set; }
    public short? DimToStarboard { get; set; }
    public decimal? Draught { get; set; }
    public string? Destination { get; set; }
    public DateTime? Eta { get; set; }
    public DateTime? FirstSeenUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}
