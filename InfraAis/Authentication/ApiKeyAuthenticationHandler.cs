
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text;
using InfraAis.Models.Auth;
using InfraAis.Repositories;
namespace InfraAis.Authentication;
public class CustomAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
   private  readonly  IApiKeyRepository _repo;
    public CustomAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,IApiKeyRepository repo) : base(options, logger, encoder)
    {
        _repo = repo ;
        
    }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string key = Request.Headers["X-API-Key"].ToString();

 if (string.IsNullOrWhiteSpace(key))
{
    string authorization = Request.Headers.Authorization.ToString();

    if (authorization.StartsWith("ApiKey ",StringComparison.OrdinalIgnoreCase))
    {
        key = authorization["ApiKey ".Length..].Trim();
    }
}

if (string.IsNullOrWhiteSpace(key))
{
    return AuthenticateResult.NoResult();
}

byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
   
    ApiKeyInfo? info = await _repo.GetActiveByHashAsync(hash);

if (info is null)
{
    return AuthenticateResult.Fail("Invalid API key");
}

        
 var claims = new List<Claim>
{
    new Claim(InfraClaimTypes.ClientId, info.ClientId.ToString()),
    new Claim(InfraClaimTypes.KeyId, info.KeyId.ToString()),
    new Claim(InfraClaimTypes.PlanId, info.PlanId.ToString()),
    new Claim(InfraClaimTypes.RateLimitPerMinute,info.RateLimitPerMinute.ToString()),
    new Claim(InfraClaimTypes.AnalyticsEnabled,info.AnalyticsEnabled ? "true" : "false")
};
if (info.MonthlyQuota.HasValue)
{
    claims.Add(new Claim(
        InfraClaimTypes.MonthlyQuota,
        info.MonthlyQuota.Value.ToString()
    ));
}

if (info.MaxHistoryDays.HasValue)
{
    claims.Add(new Claim(
        InfraClaimTypes.MaxHistoryDays,
        info.MaxHistoryDays.Value.ToString()
    ));
}

claims.Add(new Claim(ClaimTypes.Role, "ReadOnly"));
        var identity = new ClaimsIdentity(claims, Scheme.Name);

       
        var principal = new ClaimsPrincipal(identity);

       
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        
        return AuthenticateResult.Success(ticket);
    }
}
