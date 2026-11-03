
using System.Security.Claims;
using InfraAis.Authentication;
using InfraAis.Models;
using InfraAis.Models.Auth;
using InfraAis.Security;
using Xunit;

namespace InfraAis.Tests;

public class HistoryWindowGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal ApiKeyUser(string? maxDays = null)
    {
        var claims = new List<Claim>
        {
            new(InfraClaimTypes.KeyId, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, Roles.ReadOnly)
        };

        if (maxDays is not null)
            claims.Add(new Claim(InfraClaimTypes.MaxHistoryDays, maxDays));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.ApiKey));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("90", 90)]
    public void LimitedPlan_ClampsOldDates(string claim, int days)
    {
        var filters = new PositionQueryFilters { From = Now.AddDays(-180), To = Now.AddDays(5) };

        bool allowed = HistoryWindowGuard.TryApply(filters, ApiKeyUser(claim), Now);

        Assert.True(allowed);
        Assert.Equal(Now.AddDays(-days), filters.From!.Value);
        Assert.Equal(Now, filters.To!.Value);
    }

    [Fact]
    public void ValidRange_RemainsUnchanged()
    {
        var from = Now.AddHours(-5);
        var to = Now.AddHours(-1);
        var filters = new PositionQueryFilters { From = from, To = to };

        bool allowed = HistoryWindowGuard.TryApply(filters, ApiKeyUser("1"), Now);

        Assert.True(allowed);
        Assert.Equal(from, filters.From!.Value);
        Assert.Equal(to, filters.To!.Value);
    }

    [Fact]
    public void MissingDates_AreClamped()
    {
        var filters = new PositionQueryFilters();

        bool allowed = HistoryWindowGuard.TryApply(filters, ApiKeyUser("1"), Now);

        Assert.True(allowed);
        Assert.Equal(Now.AddDays(-1), filters.From!.Value);
        Assert.Equal(Now, filters.To!.Value);
    }

    [Fact]
    public void UnlimitedPlan_DoesNotChangeDates()
    {
        var from = Now.AddYears(-3);
        var filters = new PositionQueryFilters { From = from };

        bool allowed = HistoryWindowGuard.TryApply(filters, ApiKeyUser(), Now);

        Assert.True(allowed);
        Assert.Equal(from, filters.From!.Value);
        Assert.Null(filters.To);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("-5")]
    [InlineData("0")]
    public void InvalidHistoryLimit_IsRejected(string value)
    {
        var filters = new PositionQueryFilters();

        bool allowed = HistoryWindowGuard.TryApply(filters, ApiKeyUser(value), Now);

        Assert.False(allowed);
    }

    [Fact]
    public void UnauthenticatedUser_IsRejected()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        bool allowed = HistoryWindowGuard.TryApply(new PositionQueryFilters(), user, Now);

        Assert.False(allowed);
    }

    [Fact]
    public void AdminJwt_HasUnlimitedHistory()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Admin) }, "Bearer");
        var user = new ClaimsPrincipal(identity);
        var filters = new PositionQueryFilters { From = Now.AddYears(-5) };

        bool allowed = HistoryWindowGuard.TryApply(filters, user, Now);

        Assert.True(allowed);
        Assert.Equal(Now.AddYears(-5), filters.From!.Value);
    }

    [Fact]
    public void ReadOnlyJwt_IsCurrentlyRejected()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.ReadOnly) }, "Bearer");
        var user = new ClaimsPrincipal(identity);

        bool allowed = HistoryWindowGuard.TryApply(new PositionQueryFilters(), user, Now);

        Assert.False(allowed);
    }
}
