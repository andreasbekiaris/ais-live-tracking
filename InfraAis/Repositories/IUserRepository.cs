using InfraAis.Models.Auth;

namespace InfraAis.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);

    Task CreateAsync(User user);
}