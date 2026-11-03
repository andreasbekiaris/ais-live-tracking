
using InfraAis.Controllers;
using InfraAis.Models.Auth;
using InfraAis.Options;
using InfraAis.Repositories;
using InfraAis.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InfraAis.Tests;

public class AuthControllerTests
{
    private static readonly User Admin = new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Username = "admin",
        PasswordHash = "stored-hash",
        Role = "Admin"
    };

    private static AuthController Build(
        Mock<IUserRepository> repo,
        Mock<IPasswordHasher<User>> hasher,
        Mock<IJwtTokenService> jwt)
    {
        return new AuthController(
            repo.Object,
            hasher.Object,
            jwt.Object,
            Microsoft.Extensions.Options.Options.Create(new JwtOptions { ExpirationMinutes = 60 }));
    }

    [Fact]
    public async Task UnknownUsername_Returns401()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher<User>>();
        var jwt = new Mock<IJwtTokenService>();

        repo.Setup(r => r.GetByUsernameAsync("unknown")).ReturnsAsync((User?)null);

        var controller = Build(repo, hasher, jwt);
        var result = await controller.Login(new LoginRequest
        {
            Username = "unknown",
            Password = "password"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
        hasher.Verify(h => h.VerifyHashedPassword(
            It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        jwt.Verify(j => j.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher<User>>();
        var jwt = new Mock<IJwtTokenService>();

        repo.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(Admin);
        hasher.Setup(h => h.VerifyHashedPassword(Admin, Admin.PasswordHash, "wrong"))
            .Returns(PasswordVerificationResult.Failed);

        var controller = Build(repo, hasher, jwt);
        var result = await controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "wrong"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
        jwt.Verify(j => j.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CorrectPassword_ReturnsJwtToken()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher<User>>();
        var jwt = new Mock<IJwtTokenService>();

        repo.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(Admin);
        hasher.Setup(h => h.VerifyHashedPassword(Admin, Admin.PasswordHash, "correct"))
            .Returns(PasswordVerificationResult.Success);
        jwt.Setup(j => j.CreateToken(Admin)).Returns("fake.jwt.token");

        var controller = Build(repo, hasher, jwt);
        var result = await controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "correct"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);

        Assert.Equal("fake.jwt.token", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(3600, response.ExpiresInSeconds);
        jwt.Verify(j => j.CreateToken(Admin), Times.Once);
    }

    [Fact]
    public async Task SuccessfulPasswordRehashStatus_StillAllowsLogin()
    {
        var repo = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher<User>>();
        var jwt = new Mock<IJwtTokenService>();

        repo.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(Admin);
        hasher.Setup(h => h.VerifyHashedPassword(Admin, Admin.PasswordHash, "correct"))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);
        jwt.Setup(j => j.CreateToken(Admin)).Returns("fake.jwt.token");

        var controller = Build(repo, hasher, jwt);
        var result = await controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "correct"
        });

        Assert.IsType<OkObjectResult>(result);
        jwt.Verify(j => j.CreateToken(Admin), Times.Once);
    }
}
