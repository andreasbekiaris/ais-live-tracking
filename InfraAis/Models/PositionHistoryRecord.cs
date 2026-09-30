namespace InfraAis.Models;

public class PositionHistoryRecord
{

    public long Mmsi { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal? Sog { get; set; }
    public decimal? Cog { get; set; }
    public short? TrueHeading { get; set; }
    public int? NavStatus { get; set; }
    public int? RateOfTurn { get; set; }
    public bool? PositionAccuracy { get; set; }
    public DateTime MsgTimestampUtc { get; set; }
    public long Id { get; set; }
}
