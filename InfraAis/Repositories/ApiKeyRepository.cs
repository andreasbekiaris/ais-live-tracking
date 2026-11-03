using System.Data;
using InfraAis.Options;
using Microsoft.Data.SqlClient;
using InfraAis.Models.Auth;
using Microsoft.Extensions.Options;


namespace InfraAis.Repositories;



public class ApiKeyRepository : IApiKeyRepository
{

 private readonly string _connectionString;

    public ApiKeyRepository(IOptions<AisDbOptions> options)
    {
        _connectionString = options.Value.AisDb;
    }
   public async Task<ApiKeyInfo?> GetActiveByHashAsync(byte[] hash)
{
    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    const string sql = @"SELECT 
        k.id AS KeyId,
        c.id AS ClientId, 
        c.plan_id,
        p.rate_limit_per_minute,
        p.monthly_quota,
        p.max_history_days,
        p.analytics_enabled 
    FROM api_keys k
    INNER JOIN api_clients c ON k.client_id = c.id
    INNER JOIN plans p ON c.plan_id = p.id
    WHERE k.key_hash = @hash
     AND k.is_active = 1";

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash; 

    await using var reader = await cmd.ExecuteReaderAsync();
    
    if (!await reader.ReadAsync())
        return null;

    return new ApiKeyInfo
    {
        KeyId = reader.GetGuid(reader.GetOrdinal("KeyId")),
        ClientId = reader.GetGuid(reader.GetOrdinal("ClientId")),
        PlanId = reader.GetInt32(reader.GetOrdinal("plan_id")),
        RateLimitPerMinute = reader.GetInt32(reader.GetOrdinal("rate_limit_per_minute")),
        MonthlyQuota =reader.IsDBNull(reader.GetOrdinal("monthly_quota")) ? null : reader.GetInt32(reader.GetOrdinal("monthly_quota")),
        MaxHistoryDays =reader.IsDBNull(reader.GetOrdinal("max_history_days")) ? null : reader.GetInt32(reader.GetOrdinal("max_history_days")),
        AnalyticsEnabled = reader.GetBoolean(reader.GetOrdinal("analytics_enabled"))
    };
}








public async  Task CreateKeyAsync(Guid clientId, GeneratedApiKey key)
{
    await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

const string sql = @"INSERT INTO api_keys(client_id,key_hash,key_prefix)
VALUES (@clientid,@hash,@prefix)";

 
 await using var cmd = new SqlCommand(sql, conn);
 cmd.Parameters.Add("@clientid",SqlDbType.UniqueIdentifier).Value = clientId;
 cmd.Parameters.Add("@hash",SqlDbType.Binary,32).Value = key.Hash;
 cmd.Parameters.Add("@prefix",SqlDbType.Char,8).Value = key.Prefix;

await cmd.ExecuteNonQueryAsync();
}

public async Task<bool> RevokeKeyAsync(Guid keyId)
    {
        
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
const string sql = @"UPDATE api_keys
SET  is_active = 0,revoked_utc = SYSUTCDATETIME()
 WHERE id = @keyid 
 AND is_active = 1";

 await using var cmd = new SqlCommand(sql, conn);
cmd.Parameters.Add("@keyid", SqlDbType.UniqueIdentifier).Value = keyId;
int rowsAffected = await cmd.ExecuteNonQueryAsync();
return rowsAffected > 0;
    }

public async Task<List<ApiKeySummary>> ListByClientAsync(Guid clientId)
    {
        
await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"SELECT
    id AS KeyId,
    client_id AS ClientId,
    key_prefix AS KeyPrefix,
    is_active AS IsActive,
    created_utc AS CreatedUtc,
    revoked_utc AS RevokedUtc
FROM api_keys
WHERE client_id = @ClientId
ORDER BY created_utc DESC;";
List<ApiKeySummary>  apiKeys = new List<ApiKeySummary>();
 await using var cmd = new SqlCommand(sql, conn);
 cmd.Parameters.Add("@clientId", SqlDbType.UniqueIdentifier).Value = clientId;

 await using var reader = await cmd.ExecuteReaderAsync();

 while (await reader.ReadAsync())
{
     var KeyId = reader.GetGuid(reader.GetOrdinal("KeyId"));
            var ClientId = reader.GetGuid(reader.GetOrdinal("ClientId"));
            var KeyPrefix = reader.GetString(reader.GetOrdinal("KeyPrefix"));
             var IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
  var CreatedUtc = reader.GetDateTime(reader.GetOrdinal("CreatedUtc"));
             DateTime? RevokedUtc = reader.IsDBNull(reader.GetOrdinal("RevokedUtc")) ? null : reader.GetDateTime(reader.GetOrdinal("RevokedUtc"));

var apikey  = new ApiKeySummary
{
    KeyId  = KeyId,
    ClientId = ClientId,
    KeyPrefix = KeyPrefix,
IsActive = IsActive, 
CreatedUtc =CreatedUtc,
RevokedUtc = RevokedUtc

};
apiKeys.Add(apikey);

}    
return apiKeys;
    }


    }





