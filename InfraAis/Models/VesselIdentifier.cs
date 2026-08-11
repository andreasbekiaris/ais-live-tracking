namespace InfraAis.Models;

public enum IdentifierType
{
    Mmsi,
    Imo
}

public class VesselIdentifier
{
    public IdentifierType Type { get; set; }
    public long Value { get; set; }
}
