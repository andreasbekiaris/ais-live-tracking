using InfraAis.Models;

namespace InfraAis.Services;

public interface IvesselIdentifierResolver 
{
 VesselIdentifier? Parse(string rawidentifier,string? idtype);
 Task<long?> ResolveToMmsiAsync(VesselIdentifier identifier);

}