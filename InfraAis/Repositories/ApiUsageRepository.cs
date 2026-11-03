
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using InfraAis.Options;

namespace InfraAis.Repositories;

public class ApiUsageRepository : IApiUsageRepository
{
    private readonly string _connectionString;

    public ApiUsageRepository(IOptions<AisDbOptions> options)
    {
        _connectionString = options.Value.AisDb;
    }

    public async Task<long> GetMonthlyCountAsync(Guid keyId, DateTime startUtc, DateTime endUtc)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT COUNT_BIG(*)
            FROM api_usage
            WHERE api_key_id = @KeyId
              AND occurred_utc >= @StartUtc
              AND occurred_utc < @EndUtc
              AND status_code <> 429";

        var parameters = new DynamicParameters();
        parameters.Add("KeyId", keyId, DbType.Guid);
        parameters.Add("StartUtc", startUtc, DbType.DateTime2);
        parameters.Add("EndUtc", endUtc, DbType.DateTime2);

        return await conn.ExecuteScalarAsync<long>(sql, parameters);
 
    }


public async Task<long?> TryReserveAsync(Guid keyId, int? monthlyQuota, string endpoint)
{
    if (monthlyQuota < 0)
        throw new ArgumentOutOfRangeException(nameof(monthlyQuota));

    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    await using var tx = await conn.BeginTransactionAsync();

    const string lockSql = @"
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock
            @Resource = @Resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 5000;
        SELECT @result;";

    int lockResult = await conn.ExecuteScalarAsync<int>(lockSql, new { Resource = $"quota:{keyId:N}" }, tx);

    if (lockResult < 0)
    {
        await tx.RollbackAsync();
        throw new InvalidOperationException($"Quota lock failed: {lockResult}");
    }

    DateTime now = await conn.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME()", transaction: tx);
    DateTime startUtc = new DateTime(now.Year, now.Month, 1);
    DateTime endUtc = startUtc.AddMonths(1);

    if (monthlyQuota.HasValue)
    {
        const string countSql = @"
            SELECT COUNT_BIG(*)
            FROM api_usage
            WHERE api_key_id = @KeyId
              AND occurred_utc >= @StartUtc
              AND occurred_utc < @EndUtc
              AND status_code <> 429";

        long used = await conn.ExecuteScalarAsync<long>(countSql, new { KeyId = keyId, StartUtc = startUtc, EndUtc = endUtc }, tx);

        if (used >= monthlyQuota.Value)
        {
            await tx.RollbackAsync();
            return null;
        }
    }

    const string insertSql = @"
        INSERT INTO api_usage (api_key_id, endpoint, status_code, occurred_utc)
        OUTPUT INSERTED.id
        VALUES (@KeyId, @Endpoint, 0, @Now);";

    long usageId = await conn.ExecuteScalarAsync<long>(insertSql, new { KeyId = keyId, Endpoint = endpoint, Now = now }, tx);

    await tx.CommitAsync();

    return usageId;
}

public async Task CompleteUsageAsync(long usageId, int statusCode)
{
    if (statusCode < 100 || statusCode > 599)
        throw new ArgumentOutOfRangeException(nameof(statusCode));

    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    const string sql = @"
        UPDATE api_usage
        SET status_code = @StatusCode
        WHERE id = @UsageId AND status_code = 0;";

    int rows = await conn.ExecuteAsync(sql, new { UsageId = usageId, StatusCode = statusCode });

    if (rows != 1)
        throw new InvalidOperationException("Usage record was not updated");
}

}
