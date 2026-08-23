using Microsoft.AspNetCore.Mvc;

namespace InfraAis.controllers;

[ApiController]
[Route("api/vessels")]
public class vesselsController : ControllerBase
{
private  readonly IvesselIdentifierResolver _resolver;
private readonly IAisRepository _repo;
private readonly ILogger _logger;

public vesselsController(IvesselIdentifierResolver resolver,IAisRepository repo , ILogger logger)

{
_resolver = resolver ;
_repo = repo;
_logger = logger;

}

[HttpGet("{identifier}/positions/latest")]
public async Task<ActionResult<LatestPositionResponse>> GetLatestPosition(string identifier , [FromQuery] string? idtype)
{
var parsed = Parse(identifier, idtype);

if (parsed is null)
{
return BadRequest("Invalid vessel identifier");
}








}

[HttpGet("{identifier}")]


}