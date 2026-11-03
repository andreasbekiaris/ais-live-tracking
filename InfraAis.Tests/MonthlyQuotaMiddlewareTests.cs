
using System.Security.Claims;
using InfraAis.Middleware;
using InfraAis.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InfraAis.Tests;

public class MonthlyQuotaMiddlewareTests
{
    private static readonly Guid KeyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DefaultHttpContext Context(string? quota = "10", string path = "/api/positions")
    {
        var claims = new List<Claim> { new("key_id", KeyId.ToString()) };

        if (quota is not null)
            claims.Add(new Claim("monthly_quota", quota));

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static Task Run(MonthlyQuotaMiddleware middleware, DefaultHttpContext context, IApiUsageRepository repo)
    {
        return middleware.InvokeAsync(context, repo, NullLogger<MonthlyQuotaMiddleware>.Instance);
    }

    [Fact]
    public async Task AvailableQuota_ExecutesRequest_AndLogs200()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions")).ReturnsAsync((long?)42);

        bool executed = false;
        var middleware = new MonthlyQuotaMiddleware(ctx =>
        {
            executed = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        var context = Context();
        await Run(middleware, context, repo.Object);

        Assert.True(executed);
        Assert.Equal(200, context.Response.StatusCode);
        repo.Verify(r => r.CompleteUsageAsync(42, 200), Times.Once);
    }

    [Fact]
    public async Task QuotaExceeded_Returns429_WithoutExecutingRequest()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions")).ReturnsAsync((long?)null);

        bool executed = false;
        var middleware = new MonthlyQuotaMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        var context = Context();
        await Run(middleware, context, repo.Object);

        Assert.False(executed);
        Assert.Equal(429, context.Response.StatusCode);
        repo.Verify(r => r.CompleteUsageAsync(It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ForbiddenEndpoint_Logs403()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions")).ReturnsAsync((long?)50);

        var middleware = new MonthlyQuotaMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 403;
            return Task.CompletedTask;
        });

        await Run(middleware, Context(), repo.Object);

        repo.Verify(r => r.CompleteUsageAsync(50, 403), Times.Once);
    }

    [Fact]
    public async Task InvalidQuotaClaim_IsRejected()
    {
        var repo = new Mock<IApiUsageRepository>();
        var middleware = new MonthlyQuotaMiddleware(_ => Task.CompletedTask);
        var context = Context("invalid");

        await Run(middleware, context, repo.Object);

        Assert.Equal(403, context.Response.StatusCode);
        repo.Verify(r => r.TryReserveAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Enterprise_AllowsNullQuota()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, null, "/api/positions")).ReturnsAsync((long?)60);

        var middleware = new MonthlyQuotaMiddleware(_ => Task.CompletedTask);

        await Run(middleware, Context(null), repo.Object);

        repo.Verify(r => r.TryReserveAsync(KeyId, null, "/api/positions"), Times.Once);
        repo.Verify(r => r.CompleteUsageAsync(60, 200), Times.Once);
    }

    [Fact]
    public async Task EndpointException_Logs500_AndRethrows()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions")).ReturnsAsync((long?)70);

        var middleware = new MonthlyQuotaMiddleware(
            _ => throw new InvalidOperationException("Endpoint failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(middleware, Context(), repo.Object));

        repo.Verify(r => r.CompleteUsageAsync(70, 500), Times.Once);
    }

    [Fact]
    public async Task CompleteUsageFailure_DoesNotBreakSuccessfulResponse()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions")).ReturnsAsync((long?)80);
        repo.Setup(r => r.CompleteUsageAsync(80, 200))
            .ThrowsAsync(new InvalidOperationException("Database failure"));

        var middleware = new MonthlyQuotaMiddleware(_ => Task.CompletedTask);
        var context = Context();

        await Run(middleware, context, repo.Object);

        Assert.Equal(200, context.Response.StatusCode);
        repo.Verify(r => r.CompleteUsageAsync(80, 200), Times.Once);
    }

    [Fact]
    public async Task ReservationFailure_DoesNotBypassQuota()
    {
        var repo = new Mock<IApiUsageRepository>();
        repo.Setup(r => r.TryReserveAsync(KeyId, 10, "/api/positions"))
            .ThrowsAsync(new InvalidOperationException("Database failure"));

        bool executed = false;
        var middleware = new MonthlyQuotaMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(middleware, Context(), repo.Object));

        Assert.False(executed);
    }

    [Fact]
    public async Task AdminJwt_DoesNotUseApiKeyQuota()
    {
        var repo = new Mock<IApiUsageRepository>();
        var context = Context();
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }, "Bearer"));

        bool executed = false;
        var middleware = new MonthlyQuotaMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        await Run(middleware, context, repo.Object);

        Assert.True(executed);
        repo.Verify(r => r.TryReserveAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NonApiPath_DoesNotUseQuota()
    {
        var repo = new Mock<IApiUsageRepository>();
        var middleware = new MonthlyQuotaMiddleware(_ => Task.CompletedTask);

        await Run(middleware, Context("10", "/health"), repo.Object);

        repo.Verify(r => r.TryReserveAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);
    }
}
