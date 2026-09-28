namespace InfraAis.Models;

public class PositionQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Mmsi { get; set; }
    public int? Limit { get; set; }
    public string? Cursor { get; set; }
    public string? Sort { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLon { get; set; }
    public double? MaxLon { get; set; }
    public decimal? MinSog { get; set; }
    public decimal? MaxSog { get; set; }
    public string? NavStatus { get; set; }

}