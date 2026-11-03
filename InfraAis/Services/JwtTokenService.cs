
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using InfraAis.Models.Auth;
using InfraAis.Options;
using System.Text;

namespace InfraAis.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(User user)
    {
    byte[] keyBytes = Convert.FromBase64String(_options.SigningKeyBase64);

       if (keyBytes.Length < 32)
{
    throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");
}

var securityKey = new SymmetricSecurityKey(keyBytes);

var credentials = new SigningCredentials(securityKey,SecurityAlgorithms.HmacSha256);
       
var claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),

    new Claim(JwtRegisteredClaimNames.UniqueName,user.Username),

    new Claim("role", user.Role),

    new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
};
DateTime now = DateTime.UtcNow;

DateTime expires = now.AddMinutes(_options.ExpirationMinutes);

var token = new JwtSecurityToken(
    issuer: _options.Issuer,
    audience: _options.Audience,
    claims: claims,
    notBefore: now,
    expires: expires,
    signingCredentials: credentials
);

return new JwtSecurityTokenHandler().WriteToken(token);

    }
}
