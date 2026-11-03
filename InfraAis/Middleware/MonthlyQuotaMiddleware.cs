
using InfraAis.Authentication;
using InfraAis.Repositories;

namespace InfraAis.Middleware;

public class MonthlyQuotaMiddleware
{
    private readonly RequestDelegate _next;

    public MonthlyQuotaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiUsageRepository repo, ILogger<MonthlyQuotaMiddleware> logger)
    {
        if (context.User.Identity?.AuthenticationType != AuthSchemes.ApiKey || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        Guid? keyId = context.User.GetKeyId();
        int? monthlyQuota = context.User.GetMonthlyQuota();

        if (keyId is null)
        {
            context.Response.StatusCode = 403;
            return;
        }

        if (context.User.FindFirst(InfraClaimTypes.MonthlyQuota) is not null && monthlyQuota is null)
        {
            context.Response.StatusCode = 403;
            return;
        }

        string endpoint = context.Request.Path.Value ?? "/";

        long? usageId = await repo.TryReserveAsync(keyId.Value, monthlyQuota, endpoint);

        if (usageId is null)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Monthly quota exceeded");
            return;
        }

       
int statusCode = 500;

try
{
    await _next(context);
    statusCode = context.Response.StatusCode;
}
finally
{
    try
    {
        await repo.CompleteUsageAsync(usageId.Value, statusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to update usage record {UsageId}", usageId.Value);
    }
}

    }
}
