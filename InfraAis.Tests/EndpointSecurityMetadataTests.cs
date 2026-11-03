
using System.Reflection;
using InfraAis.Authentication;
using InfraAis.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace InfraAis.Tests;

public class EndpointSecurityMetadataTests
{
    [Fact]
    public void AdminClientsController_RequiresAdminPolicy()
    {
        var attributes = typeof(AdminClientsController)
            .GetCustomAttributes<AuthorizeAttribute>();

        Assert.Contains(attributes, a => a.Policy == AuthPolicies.AdminOnly);
    }

    [Fact]
    public void PositionsEndpoints_RequireReadPolicy()
    {
        var methods = new[]
        {
            nameof(PositionsController.GetPositions),
            nameof(PositionsController.GetPositionsByMmsiAsync)
        };

        foreach (var method in methods)
        {
            var action = typeof(PositionsController).GetMethod(method)!;
            var attributes = action.GetCustomAttributes<AuthorizeAttribute>();

            Assert.Contains(attributes, a => a.Policy == AuthPolicies.CanRead);
        }
    }

    [Fact]
    public void AnalyticsEndpoint_RequiresAnalyticsPolicy()
    {
        var action = typeof(PositionsController)
            .GetMethod(nameof(PositionsController.GetVesselAnalyticsAsync))!;

        var attributes = action.GetCustomAttributes<AuthorizeAttribute>();

        Assert.Contains(attributes, a => a.Policy == AuthPolicies.AnalyticsAccess);
    }

    [Fact]
    public void VesselsController_RequiresReadPolicy()
    {
        var attributes = typeof(VesselsController)
            .GetCustomAttributes<AuthorizeAttribute>();

        Assert.Contains(attributes, a => a.Policy == AuthPolicies.CanRead);
    }

    [Fact]
    public void PositionsController_HasApiKeyRateLimiting()
    {
        var attribute = typeof(PositionsController)
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RateLimitPolicies.ApiKeyPerMinute, attribute.PolicyName);
    }

    [Fact]
    public void VesselsController_HasApiKeyRateLimiting()
    {
        var attribute = typeof(VesselsController)
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RateLimitPolicies.ApiKeyPerMinute, attribute.PolicyName);
    }

    [Fact]
    public void Login_HasLoginRateLimiting()
    {
        var action = typeof(AuthController).GetMethod(nameof(AuthController.Login))!;
        var attribute = action.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RateLimitPolicies.Login, attribute.PolicyName);
    }
}
