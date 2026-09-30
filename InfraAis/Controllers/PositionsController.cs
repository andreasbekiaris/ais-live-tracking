namespace InfraAis.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InfraAis.Utils;

using InfraAis.Models;
using InfraAis.Services;
using InfraAis.Repositories;
using InfraAis.Validation;


[ApiController]
[Route("api/positions")]
public class PositionsController : ControllerBase
{
   
    private readonly IAisRepository _repo;
    private readonly ILogger<PositionsController> _logger;
    private readonly IPositionQueryValidator _positionQueryValidator ;
    private readonly IvesselIdentifierResolver _resolver;

    public PositionsController(IAisRepository repo, ILogger<PositionsController> logger,IvesselIdentifierResolver resolver, IPositionQueryValidator positionQueryValidator)
    {
        
        _repo = repo;
        _logger = logger;
        _resolver = resolver;
        _positionQueryValidator =positionQueryValidator;
    }
    
[HttpGet]
public async Task<ActionResult<PagedResponse>> GetPositions(
    
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] string? navstatus,
    [FromQuery] decimal? minsog,
    [FromQuery] decimal? maxsog,
    [FromQuery] string? mmsi,
    [FromQuery] string? sort,
    [FromQuery] int? limit,
    [FromQuery] string? cursor,
    [FromQuery] double? minlat,
    [FromQuery] double? maxlat,
    [FromQuery] double? minlon,
    [FromQuery] double? maxlon)
 
{
var query = new PositionQuery
{
    From = from,
    To = to,
    Mmsi = mmsi,
    Limit = limit,
    Cursor = cursor,
    Sort = sort,
    MinLat = minlat,
    MaxLat = maxlat,
    MinLon = minlon,
    MaxLon = maxlon,
    MinSog = minsog,
    MaxSog = maxsog,
    NavStatus = navstatus
};
var result = _positionQueryValidator.Validate(query);
      if(!result.IsValid)
      {
        return BadRequest(new { error = result.Error });
      }
      else {
        
       var page =  await _repo.GetPositionsAsync(result.Value!);

    return Ok(page);
      }


}

[HttpGet ("/api/vessels/{identifier}/positions")]
public async Task<ActionResult<PagedResponse>> GetPositionsByMmsiAsync(string identifier,[FromQuery] string? idtype,[FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] string? navstatus,
    [FromQuery] decimal? minsog,
    [FromQuery] decimal? maxsog,
    [FromQuery] string? sort,
    [FromQuery] int? limit,
    [FromQuery] string? cursor,
    [FromQuery] double? minlat,
    [FromQuery] double? maxlat,
    [FromQuery] double? minlon,
    [FromQuery] double? maxlon)
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

        if (await _repo.GetVesselAsync(mmsi.Value) is null)
        {
            _logger.LogWarning("NOT FOUND: unknown MMSI={Mmsi}", mmsi.Value);
            return NotFound(new { error = "No vessel known with this MMSI." });
        }

        var query = new PositionQuery
{
    From = from,
    To = to,
    Limit = limit,
    Cursor = cursor,
    Sort = sort,
    MinLat = minlat,
    MaxLat = maxlat,
    MinLon = minlon,
    MaxLon = maxlon,
    MinSog = minsog,
    MaxSog = maxsog,
    NavStatus = navstatus
};

var result = _positionQueryValidator.Validate(query, isFleet: false);
      if(!result.IsValid)
      {
        return BadRequest(new { error = result.Error });
      }
      else {

       var page =  await _repo.GetPositionsByMmsiAsync(mmsi.Value, result.Value!);
       return Ok(page);

    }

}
}