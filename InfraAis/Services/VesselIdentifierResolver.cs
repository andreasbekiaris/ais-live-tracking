using InfraAis.Models;
using InfraAis.Repositories;

namespace InfraAis.Services;

public interface IVesselIdentifierResolver
{
    VesselIdentifier? Parse(string rawIdentifier, string? idTypeOverride);

    Task<long?> ResolveToMmsiAsync(VesselIdentifier identifier);
}

public class VesselIdentifierResolver : IVesselIdentifierResolver
{
    private readonly IAisRepository _repo;

    public VesselIdentifierResolver(IAisRepository repo)
    {
        _repo = repo;
    }

    public VesselIdentifier? Parse(string rawIdentifier, string? idTypeOverride)
    {
        if (!long.TryParse(rawIdentifier, out var value) || value <= 0)
            return null;

        var digits = rawIdentifier.Trim().Length;

        if (idTypeOverride is not null)
        {
            switch (idTypeOverride.ToLowerInvariant())
            {
                case "mmsi":
                    return digits == 9 ? new VesselIdentifier { Type = IdentifierType.Mmsi, Value = value } : null;
                case "imo":
                    return digits == 7 ? new VesselIdentifier { Type = IdentifierType.Imo, Value = value } : null;
                default:
                    return null;
            }
        }

        return digits switch
        {
            9 => new VesselIdentifier { Type = IdentifierType.Mmsi, Value = value },
            7 => new VesselIdentifier { Type = IdentifierType.Imo, Value = value },
            _ => null
        };
    }

    public async Task<long?> ResolveToMmsiAsync(VesselIdentifier identifier)
    {
        if (identifier.Type == IdentifierType.Mmsi)
            return identifier.Value;

        return await _repo.GetMmsiByImoAsync((int)identifier.Value);
    }
}
