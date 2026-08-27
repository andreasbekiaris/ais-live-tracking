using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InfraAis.Utils;
using InfraAis.Models;
using InfraAis.Services;
using InfraAis.Repositories;
namespace InfraAis.Controllers;

[ApiController]
[Route("api/vessels")]
public class VesselsController : ControllerBase
{
    private readonly IvesselIdentifierResolver _resolver;
    private readonly IAisRepository _repo;
    private readonly ILogger<VesselsController> _logger;

    public VesselsController(IvesselIdentifierResolver resolver, IAisRepository repo, ILogger<VesselsController> logger)

    {
        _resolver = resolver;
        _repo = repo;
        _logger = logger;

    }

    [HttpGet("{identifier}/positions/latest")]
    public async Task<ActionResult<LatestPositionResponse?>> GetLatestPosition(string identifier, [FromQuery] string? idtype)
    {
        var parsed = _resolver.Parse(identifier, idtype);

        if (parsed is null)
        {
            _logger.LogWarning("REJECT: malformed identifier '{Identifier}' idType={IdType}", identifier, idtype);
            return BadRequest(new { error = "Identifier must be a 7-digit IMO or 9-digit MMSI; idType must match." });
        }

        var mmsi = await _resolver.ResolveToMmsiAsync(parsed);

        if (mmsi is null)
        {
            _logger.LogWarning("NOT FOUND: unknown IMO={Imo}", parsed.Value);
            return NotFound(new { error = "No vessel known with this IMO." });
        }


        var position = await _repo.GetLatestPositionAsync(mmsi.Value);
        if (position is null)
        {
            if (await _repo.GetVesselAsync(mmsi.Value) is null)
            {
                _logger.LogWarning("NOT FOUND: unknown MMSI={Mmsi}", mmsi.Value);
                return NotFound(new { error = "No vessel known with this MMSI." });
            }
            _logger.LogWarning("NOT FOUND: no positions for MMSI={Mmsi}", mmsi.Value);
            return NotFound(new { error = "Vessel is known but has no stored positions yet." });
        }

        var AgeSeconds = (DateTime.UtcNow - position.TimestampUtc).TotalSeconds;
        return Ok(new LatestPositionResponse
        {
            Mmsi = position.Mmsi,
            Imo = position.Imo,
            Name = position.Name,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Sog = position.Sog,
            Cog = position.Cog,
            NavStatus = position.NavStatus,
            NavStatusText = NavStatusMapper.ToText(position.NavStatus),
            TimestampUtc = position.TimestampUtc,
            AgeSeconds = AgeSeconds
        });



    }
    [HttpGet("{identifier}")]
    public async Task<ActionResult<VesselResponse?>> GetVessel(string identifier, [FromQuery] string? idtype)
    {
        var parsed = _resolver.Parse(identifier, idtype);

        if (parsed is null)
            return BadRequest(new { error = "Identifier must be a 7-digit IMO or 9-digit MMSI; idType must match." });

        var mmsi = await _resolver.ResolveToMmsiAsync(parsed);
        if (mmsi is null)
            return NotFound(new { error = "No vessel known with this IMO." });

        var vessel = await _repo.GetVesselAsync(mmsi.Value);
        if (vessel is null)
            return NotFound(new { error = "No vessel known with this MMSI." });

        return Ok(new VesselResponse
        {
            Mmsi = vessel.Mmsi,
            Imo = vessel.Imo,
            Name = vessel.Name,
            CallSign = vessel.CallSign,
            ShipType = vessel.ShipType,
            DimToBow = vessel.DimToBow,
            DimToStern = vessel.DimToStern,
            DimToPort = vessel.DimToPort,
            DimToStarboard = vessel.DimToStarboard,
            Draught = vessel.Draught,
            Destination = vessel.Destination,
            Eta = vessel.Eta,
            FirstSeenUtc = vessel.FirstSeenUtc,
            LastSeenUtc = vessel.LastSeenUtc
        });

    }
}