
using Microsoft.AspNetCore.Identity;
using InfraAis.Models.Auth;
using InfraAis.Repositories;

namespace InfraAis.Security;

public static class AdminUserSeeder
{
    public static async Task SeedAsync(
        IUserRepository repo,
        IPasswordHasher<User> hasher,
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Missing admin credentials");
        }

        User? existingUser = await repo.GetByUsernameAsync(username);

        if (existingUser is not null)
        {
            return;
        }

        User newUser = new User
        {
            Username = username,
            Role = "Admin"
        };

        string passwordHash = hasher.HashPassword(newUser, password);

        newUser.PasswordHash = passwordHash;

        await repo.CreateAsync(newUser);
    }
}
