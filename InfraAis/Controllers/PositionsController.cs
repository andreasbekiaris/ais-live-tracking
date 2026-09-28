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
    private readonly IvesselIdentifierResolver _resolver;
    private readonly IAisRepository _repo;
    private readonly ILogger<PositionsController> _logger;

    public PositionsController(IvesselIdentifierResolver resolver, IAisRepository repo, ILogger<PositionsController> logger)
    {
        _resolver = resolver;
        _repo = repo;
        _logger = logger;
    }

    public async Task<ActionResult<?>> GetPositions(string identifier, [FromQuery] string? idtype)
    {
        

}}