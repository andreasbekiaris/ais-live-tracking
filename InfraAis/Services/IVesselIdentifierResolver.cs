namespace InfraAis.Services;

public interface IvesselIdentifierResolver 
{
 VesselIdentifier? Parse(string rawidentifier,string? idtype);
 Task<long?> ResolveToMmsi(VesselIdentifier identifier);

}