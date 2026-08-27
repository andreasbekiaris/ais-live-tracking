namespace InfraAis.Services;

using InfraAis.Repositories;
using InfraAis.Models;



public class VesselIdentifierResolver : IvesselIdentifierResolver
{
    private readonly IAisRepository _repo;
    public VesselIdentifierResolver(IAisRepository repo)
    {
        _repo = repo;
    }

    public VesselIdentifier? Parse(string rawidentifier, string? idtypeoveride)
    {
        if (!long.TryParse(rawidentifier, out long value))
        {
            return null;
        }
        var digits = rawidentifier.Trim().Length;

        if (idtypeoveride is not null)
        {
            switch (idtypeoveride.ToLowerInvariant())
            {
                case "mmsi":
                    return digits == 9 ? new VesselIdentifier { Type = IdentifierType.Mmsi, Value = value } : null;
                case "imo":
                    return digits == 7 ? new VesselIdentifier { Type = IdentifierType.Imo, Value = value } : null;
                default:
                    return null;
            }
        }
        else
        {
            switch (digits)
            {
                case 9:
                    return new VesselIdentifier { Type = IdentifierType.Mmsi, Value = value };
                case 7:
                    return new VesselIdentifier { Type = IdentifierType.Imo, Value = value };
                default:
                    return null;
            }
        }


    }
    public async Task<long?> ResolveToMmsiAsync(VesselIdentifier identifier)
    {
        if (identifier.Type == IdentifierType.Mmsi)
        {
            return identifier.Value;
        }
        return await _repo.GetMmsiByImoAsync((int)identifier.Value);
    }
}