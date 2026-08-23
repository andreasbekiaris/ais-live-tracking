namespace InfraAis.Services;



public class  VesselIdentifierResolver : IvesselIdentifierResolver
{
    private readonly IAisRepository _repo ;
public VesselIdentifierResolver(IAisRepository repo  , ILogger logger)
{
_repo = repo ;
_logger = logger;
}

public VesselIdentifier? Parse(string rawidentifier, string? idtypeoveride)
{
if (!long.TryParse(rawidentifier, out long value))
{
    _logger.LogWarning("Invalid MMSI OR IMO: {Raw}", message.Mmsi);
    return null; 
    
}
var digits = Value.trim().length;

if(idtypeoveride is not null)
{
switch(idtypeoveride.ToLowerInvariant())
{
    case "mmsi":
    return digits == 9 ? new VesselIdentifier{Type = IdentifierType.Mmsi,Value = value}: null ;
    case "imo":
    return digits == 7 ? new VesselIdentifier{Type = IdentifierType.Imo,Value = value}: null;
    default:
    return null;
}
}
else
{
switch (digits)
{
    case 9:
return  new VesselIdentifier{Type = IdentifierType.Mmsi,Value = value} ;
case 7:
return  new VesselIdentifier{Type = IdentifierType.Imo,Value = value} ;
default:
return null;
}
}


}
public async task<long?> ResolveToMmsi(VesselIdentifier identifier)
{
if(identifier.Type == IdentifierType.Mmsi)
{
    return  identifier.Value;
}
return await_repo.GetMmsiByImoAsync((int) identifier.Value);
}
}