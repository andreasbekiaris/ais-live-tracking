
using System.Text.Json;
using InfraAis.Controllers;
using InfraAis.Models.Auth;
using InfraAis.Repositories;
using InfraAis.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace InfraAis.Tests;

public class AdminClientsControllerTests
{
    private static readonly Guid ClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static AdminClientsController Build(
        Mock<IApiClientRepository> clients,
        Mock<IApiKeyRepository> keys)
    {
        return new AdminClientsController(clients.Object, keys.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static ApiClient Client() => new()
    {
        Id = ClientId,
        Name = "Test Client",
        PlanId = 1
    };

    [Fact]
    public async Task CreateClient_InvalidPlan_Returns400()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.PlanExistsAsync(999)).ReturnsAsync(false);

        var result = await Build(clients, keys).CreateClient(
            new CreateApiClientRequest { Name = "Test", PlanId = 999 });

        Assert.IsType<BadRequestObjectResult>(result);
        clients.Verify(r => r.CreateAsync(It.IsAny<ApiClient>()), Times.Never);
    }

    [Fact]
    public async Task CreateClient_ValidPlan_Returns201()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();

        clients.Setup(r => r.PlanExistsAsync(1)).ReturnsAsync(true);
        clients.Setup(r => r.CreateAsync(It.IsAny<ApiClient>())).ReturnsAsync(ClientId);

        var result = await Build(clients, keys).CreateClient(
            new CreateApiClientRequest { Name = "Test Client", PlanId = 1 });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Contains(ClientId.ToString(), JsonSerializer.Serialize(created.Value));

        clients.Verify(r => r.CreateAsync(It.Is<ApiClient>(
            c => c.Name == "Test Client" && c.PlanId == 1)), Times.Once);
    }

    [Fact]
    public async Task GetClients_Returns200WithList()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ApiClient> { Client() });

        var result = await Build(clients, keys).GetClients();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<ApiClient>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(ClientId, list[0].Id);
    }

    [Fact]
    public async Task GetClient_UnknownId_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync((ApiClient?)null);

        var result = await Build(clients, keys).GetClient(ClientId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetClient_KnownId_Returns200()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync(Client());

        var result = await Build(clients, keys).GetClient(ClientId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(ClientId, Assert.IsType<ApiClient>(ok.Value).Id);
    }

    [Fact]
    public async Task ChangePlan_NonPositivePlan_Returns400()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();

        var result = await Build(clients, keys).ChangePlan(
            ClientId, new ChangePlanRequest(0));

        Assert.IsType<BadRequestObjectResult>(result);
        clients.Verify(r => r.ChangePlanAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChangePlan_UnknownClient_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync((ApiClient?)null);

        var result = await Build(clients, keys).ChangePlan(
            ClientId, new ChangePlanRequest(2));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync(Client());
        clients.Setup(r => r.PlanExistsAsync(999)).ReturnsAsync(false);

        var result = await Build(clients, keys).ChangePlan(
            ClientId, new ChangePlanRequest(999));

        Assert.IsType<NotFoundObjectResult>(result);
        clients.Verify(r => r.ChangePlanAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChangePlan_ValidRequest_Returns204()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync(Client());
        clients.Setup(r => r.PlanExistsAsync(2)).ReturnsAsync(true);

        var result = await Build(clients, keys).ChangePlan(
            ClientId, new ChangePlanRequest(2));

        Assert.IsType<NoContentResult>(result);
        clients.Verify(r => r.ChangePlanAsync(ClientId, 2), Times.Once);
    }

    [Fact]
    public async Task CreateApiKey_UnknownClient_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync((ApiClient?)null);

        var result = await Build(clients, keys).CreateApiKey(ClientId);

        Assert.IsType<NotFoundObjectResult>(result);
        keys.Verify(r => r.CreateKeyAsync(
            It.IsAny<Guid>(), It.IsAny<GeneratedApiKey>()), Times.Never);
    }

    [Fact]
    public async Task CreateApiKey_ValidClient_Returns201AndNoStore()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync(Client());

        var controller = Build(clients, keys);
        var result = await controller.CreateApiKey(ClientId);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Contains("ais_", JsonSerializer.Serialize(created.Value));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());

        keys.Verify(r => r.CreateKeyAsync(
            ClientId, It.IsAny<GeneratedApiKey>()), Times.Once);
    }

    [Fact]
    public async Task GetClientKeys_UnknownClient_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync((ApiClient?)null);

        var result = await Build(clients, keys).GetClientKeys(ClientId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetClientKeys_KnownClient_Returns200()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        clients.Setup(r => r.GetByIdAsync(ClientId)).ReturnsAsync(Client());
        keys.Setup(r => r.ListByClientAsync(ClientId))
            .ReturnsAsync(new List<ApiKeySummary>());

        var result = await Build(clients, keys).GetClientKeys(ClientId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsType<List<ApiKeySummary>>(ok.Value));
        keys.Verify(r => r.ListByClientAsync(ClientId), Times.Once);
    }

    [Fact]
    public async Task RevokeKey_ExistingKey_Returns204()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        Guid keyId = Guid.NewGuid();
        keys.Setup(r => r.RevokeKeyAsync(keyId)).ReturnsAsync(true);

        var result = await Build(clients, keys).RevokeKey(keyId);

        Assert.IsType<NoContentResult>(result);
        keys.Verify(r => r.RevokeKeyAsync(keyId), Times.Once);
    }

    [Fact]
    public async Task RevokeKey_UnknownKey_Returns404()
    {
        var clients = new Mock<IApiClientRepository>();
        var keys = new Mock<IApiKeyRepository>();
        Guid keyId = Guid.NewGuid();
        keys.Setup(r => r.RevokeKeyAsync(keyId)).ReturnsAsync(false);

        var result = await Build(clients, keys).RevokeKey(keyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}

