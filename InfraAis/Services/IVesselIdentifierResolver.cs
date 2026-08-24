namespace InfraAis.Services;
using InfraAis.Models;
public interface IvesselIdentifierResolver 
{
 VesselIdentifier? Parse(string rawidentifier,string? idtype);
 Task<long?> ResolveToMmsiAsync(VesselIdentifier identifier);

}