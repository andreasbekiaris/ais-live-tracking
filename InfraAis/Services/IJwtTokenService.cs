using InfraAis.Models.Auth;

namespace InfraAis.Services;

public interface IJwtTokenService
{
    string CreateToken(User user);
}