namespace InfraAis.Repositories;

public interface IApiUsageRepository
{
    Task<long> GetMonthlyCountAsync(Guid keyId, DateTime startUtc, DateTime endUtc);
    Task<long?> TryReserveAsync(Guid keyId, int? monthlyQuota, string endpoint);
    Task CompleteUsageAsync(long usageId, int statusCode);
}