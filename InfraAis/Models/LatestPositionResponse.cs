namespace InfraAis.Models;

public class LatestPositionResponse
{
  public long Mmsi { get; set; }
    public int? Imo { get; set; }
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal? Sog { get; set; }
    public decimal? Cog { get; set; }
    public int? NavStatus { get; set; }
    public string NavStatusText { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public double AgeSeconds { get; set; }
    
}