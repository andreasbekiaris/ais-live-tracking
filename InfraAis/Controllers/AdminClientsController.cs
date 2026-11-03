
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InfraAis.Authentication;
using InfraAis.Models.Auth;
using InfraAis.Security;
using InfraAis.Repositories;

namespace InfraAis.Controllers;

[ApiController]
[Route("api/admin/clients")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class AdminClientsController : ControllerBase
{
    private readonly IApiClientRepository _repo;
private readonly IApiKeyRepository _repoApikey;
    public AdminClientsController(IApiClientRepository repo, IApiKeyRepository repoApikey)
    {
        _repo = repo;
        _repoApikey = repoApikey;
    }

    [HttpPost]
    public async Task<IActionResult> CreateClient( [FromBody] CreateApiClientRequest request)
    {

if (!await _repo.PlanExistsAsync(request.PlanId))
{
    return BadRequest("Plan does not exist");
}

ApiClient newuser = new ApiClient {
     Name = request.Name ,
      PlanId = request.PlanId
      };

Guid newId = await _repo.CreateAsync(newuser);

  return StatusCode(StatusCodes.Status201Created,new { id = newId });
    }



[HttpGet]
public async Task<IActionResult> GetClients()
{
  List<ApiClient> all = await _repo.GetAllAsync();
   return Ok(all);
}

[HttpGet("{id:guid}")]
public async Task<IActionResult> GetClient(Guid id)
{
    var client = await _repo.GetByIdAsync(id);
     if(client is not null)
     return Ok(client);

return NotFound($"client with this id : {id} does not exist");
 
}



[HttpPut("{id:guid}/plan")]
public async Task<IActionResult> ChangePlan( Guid id,[FromBody] ChangePlanRequest request)
{
    if(request.PlanId <= 0 )
    return BadRequest("the planid is negative");

    var client = await _repo.GetByIdAsync(id);
if(client is null)
return NotFound($"there is no client with this id {id}");


if(!await _repo.PlanExistsAsync(request.PlanId))
    return NotFound($"this plan : {request.PlanId} does not exist");

  await _repo.ChangePlanAsync(id,request.PlanId);

    return NoContent();

 
}
[HttpPost("{id:guid}/keys")]
public async Task<IActionResult> CreateApiKey(Guid id)
{
    var client = await _repo.GetByIdAsync(id);
if(client is null)
return NotFound($"there is no client with this id {id}");

    GeneratedApiKey apiKey = ApiKeyGenerator.GenerateApiKey();

  await _repoApikey.CreateKeyAsync(client.Id, apiKey);
Response.Headers.CacheControl = "no-store";
    return StatusCode(StatusCodes.Status201Created,new  {
        ApiKey = apiKey.RawKey,
        Prefix = apiKey.Prefix
    });
}
[HttpGet("{id:guid}/keys")]
public async Task<IActionResult> GetClientKeys(Guid id)
{
 var client = await _repo.GetByIdAsync(id);

    if (client is null)
        return NotFound($"Client with ID {id} does not exist");

    List<ApiKeySummary> generatedApiKeys = await _repoApikey.ListByClientAsync(client.Id);

    return Ok(generatedApiKeys);
}

[HttpPost("/api/admin/keys/{id:guid}/revoke")]
public async Task<IActionResult> RevokeKey(Guid id)
{
  var revoked =  await _repoApikey.RevokeKeyAsync(id);


 if(!revoked)
 return  NotFound($"No active API key found with this ID : {id}");

  return NoContent();
}
}

