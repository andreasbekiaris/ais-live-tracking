using Microsoft.AspNetCore.Mvc;
using InfraAis.Models;
using InfraAis.Repositories;
using InfraAis.Services;
using InfraAis.Utils;

namespace InfraAis.Controllers;

[ApiController]
[Route("api/vessels")]
public class VesselsController : ControllerBase
{
    private readonly IVesselIdentifierResolver _resolver;
    private readonly IAisRepository _repo;
    private readonly ILogger<VesselsController> _logger;

    public VesselsController(
        IVesselIdentifierResolver resolver,
        IAisRepository repo,
        ILogger<VesselsController> logger)
    {
        _resolver = resolver;
        _repo = repo;
        _logger = logger;
    }

    [HttpGet("{identifier}/positions/latest")]
    public async Task<ActionResult<LatestPositionResponse>> GetLatestPosition(
        string identifier, [FromQuery] string? idType)
    {
        var parsed = _resolver.Parse(identifier, idType);
        if (parsed is null)
        {
            _logger.LogWarning("Malformed identifier: {Identifier} (idType={IdType})", identifier, idType);
            return BadRequest(new { error = "Identifier must be a 7-digit IMO or 9-digit MMSI; idType must match." });
        }

        var mmsi = await _resolver.ResolveToMmsiAsync(parsed);
        if (mmsi is null)
        {
            _logger.LogWarning("Unknown IMO: {Imo}", parsed.Value);
            return NotFound(new { error = "No vessel known with this IMO." });
        }

        var latest = await _repo.GetLatestPositionAsync(mmsi.Value);
        if (latest is null)
        {
            if (await _repo.GetVesselAsync(mmsi.Value) is null)
            {
                _logger.LogWarning("Unknown MMSI: {Mmsi}", mmsi.Value);
                return NotFound(new { error = "No vessel known with this MMSI." });
            }

            _logger.LogWarning("No positions stored for MMSI: {Mmsi}", mmsi.Value);
            return NotFound(new { error = "Vessel is known but has no stored positions yet." });
        }

        return Ok(new LatestPositionResponse
        {
            Mmsi = latest.Mmsi,
            Imo = latest.Imo,
            Name = latest.Name,
            Latitude = latest.Latitude,
            Longitude = latest.Longitude,
            Sog = latest.Sog,
            Cog = latest.Cog,
            NavStatus = latest.NavStatus,
            NavStatusText = NavStatusMapper.ToText(latest.NavStatus),
            TimestampUtc = latest.MsgTimestampUtc,
            AgeSeconds = Math.Round((DateTime.UtcNow - latest.MsgTimestampUtc).TotalSeconds)
        });
    }

    [HttpGet("{identifier}")]
    public async Task<ActionResult<VesselInfoResponse>> GetVesselInfo(
        string identifier, [FromQuery] string? idType)
    {
        var parsed = _resolver.Parse(identifier, idType);
        if (parsed is null)
            return BadRequest(new { error = "Identifier must be a 7-digit IMO or 9-digit MMSI; idType must match." });

        var mmsi = await _resolver.ResolveToMmsiAsync(parsed);
        if (mmsi is null)
            return NotFound(new { error = "No vessel known with this IMO." });

        var vessel = await _repo.GetVesselAsync(mmsi.Value);
        if (vessel is null)
            return NotFound(new { error = "No vessel known with this MMSI." });

        return Ok(new VesselInfoResponse
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
