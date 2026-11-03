
using System.Security.Claims;
using InfraAis.Authentication;
using InfraAis.Models;
using InfraAis.Models.Auth;

namespace InfraAis.Security;

public static class HistoryWindowGuard
{
 public static bool TryApply(PositionQueryFilters filters, ClaimsPrincipal user, DateTimeOffset now)
{
    if (user.Identity?.IsAuthenticated != true)
        return false;

    if (user.IsInRole(Roles.Admin) && user.GetKeyId() is null)
        return true;

    bool isApiKeyUser = user.Identity?.AuthenticationType == AuthSchemes.ApiKey;

    if (!isApiKeyUser)
        return false;

    if (!user.TryGetMaxHistoryDays(out int? maxDays))
        return false;

    if (!maxDays.HasValue || maxDays.Value <= 0)
        return false;

    DateTimeOffset earliest = now.AddDays(-maxDays.Value);

    if (!filters.From.HasValue || filters.From.Value < earliest)
        filters.From = earliest;

    if (!filters.To.HasValue || filters.To.Value > now)
        filters.To = now;

    return filters.From.Value < filters.To.Value;
}
}

