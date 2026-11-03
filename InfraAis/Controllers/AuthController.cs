
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using InfraAis.Models.Auth;
using InfraAis.Repositories;
using Microsoft.Extensions.Options;
using InfraAis.Services;
using InfraAis.Options;
using InfraAis.Authentication;
using Microsoft.AspNetCore.RateLimiting;
namespace InfraAis.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _repo;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IJwtTokenService _jwtService;
private readonly JwtOptions _jwtOptions;
    public AuthController(IUserRepository repo, IPasswordHasher<User> hasher,IJwtTokenService jwtService, IOptions<JwtOptions> jwtOptions)
    {
        _repo = repo;
        _hasher = hasher;
        _jwtService = jwtService;
         _jwtOptions = jwtOptions.Value;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
User? user = await _repo.GetByUsernameAsync(request.Username);
if(user is null)
        {
            return Unauthorized(new { message = "Invalid credentials" });

        }
PasswordVerificationResult result = _hasher.VerifyHashedPassword(user,user.PasswordHash, request.Password);

if (result == PasswordVerificationResult.Failed)
{
    return Unauthorized(new {message = "Invalid credentials"});
}

string token = _jwtService.CreateToken(user);
LoginResponse response = new LoginResponse
{
    AccessToken = token,
    TokenType = "Bearer",
    ExpiresInSeconds = _jwtOptions.ExpirationMinutes *60

};
       return Ok(response);
    }
}
