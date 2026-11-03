using System.Security.Claims;

namespace InfraAis.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetClientId(this ClaimsPrincipal user)
    {
        string? value = user.FindFirst(InfraClaimTypes.ClientId)?.Value;

        if (Guid.TryParse(value, out Guid clientId))
        {
            return clientId;
        }

        return null;
    }
    public static Guid? GetKeyId(this ClaimsPrincipal user)
{
string? value = user.FindFirst(InfraClaimTypes.KeyId)?.Value;
 if (Guid.TryParse(value, out Guid keyid))
        {
            return keyid;
        }
         return null;
}

public static int? GetRateLimit(this ClaimsPrincipal user)
{
    string? value = user.FindFirst(InfraClaimTypes.RateLimitPerMinute)?.Value;
 if (int.TryParse(value, out int RateLimitPerMinute))
        {
            return RateLimitPerMinute;
        }
         return null;
}

public static bool HasAnalyticsAccess(this ClaimsPrincipal user)
{
      string? value = user.FindFirst(InfraClaimTypes.AnalyticsEnabled)?.Value;
 if (bool.TryParse(value, out bool AnalyticsEnabled))
        {
            return AnalyticsEnabled;
        }
         return false;
}
public static int? GetMonthlyQuota(this ClaimsPrincipal user)
{
    string? value = user.FindFirst(InfraClaimTypes.MonthlyQuota)?.Value;
 if (int.TryParse(value, out int MonthlyQuota))
        {
            return MonthlyQuota;
        }
         return null;
}
public static bool TryGetMaxHistoryDays(this ClaimsPrincipal user, out int? maxDays)
{
    maxDays = null;

    Claim? claim = user.FindFirst(InfraClaimTypes.MaxHistoryDays);

    if (claim is null)
        return true;

    if (!int.TryParse(claim.Value, out int days) || days <= 0)
        return false;

    maxDays = days;
    return true;
}

}
