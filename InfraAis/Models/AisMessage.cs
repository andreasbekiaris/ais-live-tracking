using System.Text.Json.Serialization;

namespace InfraAis.Models;


public class AisMessage
{
    public string MessageType { get; set; } = "";
    public MessageBody Message { get; set; } = new();
    public MetaData MetaData { get; set; } = new();
}


public class MessageBody
{
    public PositionReport? PositionReport { get; set; }
    public ShipStaticData? ShipStaticData { get; set; }
}

public class PositionReport
{
    public long UserID { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Sog { get; set; }
    public double Cog { get; set; }
    public int TrueHeading { get; set; }
    public int NavigationalStatus { get; set; }
    public int RateOfTurn { get; set; }
    public bool PositionAccuracy { get; set; }
    public bool Valid { get; set; }
}

public class ShipStaticData
{
    public long UserID { get; set; }
    public int ImoNumber { get; set; }
    public string CallSign { get; set; } = "";
    public string Name { get; set; } = "";
    public int Type { get; set; }
}

public class MetaData
{
    public long MMSI { get; set; }
    public string ShipName { get; set; } = "";

    [JsonPropertyName("time_utc")]
    public string TimeUtc { get; set; } = "";
}