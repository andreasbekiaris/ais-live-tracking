using System.Runtime.CompilerServices;
using InfraAis.Models.Auth;
using System.Data;
using Microsoft.Data.SqlClient;
using InfraAis.Options;
using Microsoft.Extensions.Options;

namespace InfraAis.Repositories;



public class ApiClientRepository : IApiClientRepository
{
    
private readonly string _connectionString;
public ApiClientRepository(IOptions<AisDbOptions> options)
    {
         _connectionString = options.Value.AisDb;
    }



public async Task<Guid> CreateAsync(ApiClient client)
    {
        await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

const string sql = @"INSERT INTO api_clients (name,plan_id) OUTPUT INSERTED.id VALUES (@name,@planid) ";


await using var cmd = new SqlCommand(sql, conn);


 cmd.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = client.Name; 
 cmd.Parameters.Add("@planid", SqlDbType.Int).Value = client.PlanId; 


object? result = await cmd.ExecuteScalarAsync();

if (result is not Guid id)
{
   return Guid.Empty;
}

return id;
    }




public async Task<ApiClient?> GetByIdAsync(Guid clientId)
    {
        await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync(); 

const string sql = @"SELECT id ,name, plan_id,created_utc FROM api_clients WHERE id = @Id ";
      await using var cmd = new SqlCommand(sql, conn);
cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = clientId;

await using var reader = await cmd.ExecuteReaderAsync();
if (!await reader.ReadAsync())
{
    return null;
}

return new ApiClient
{
   Id = reader.GetGuid(reader.GetOrdinal("id")),
  Name = reader.GetString(reader.GetOrdinal("name")),
    PlanId = reader.GetInt32(reader.GetOrdinal("plan_id")),
    CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))
};


    }

public async Task<List<ApiClient>> GetAllAsync()
    {
         await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync(); 

const string sql = @"SELECT * FROM api_clients ORDER BY created_utc DESC";
List<ApiClient> clients = new List<ApiClient>();
       await using var cmd = new SqlCommand(sql, conn);

await using var reader = await cmd.ExecuteReaderAsync();


while (await reader.ReadAsync())
{
     var id = reader.GetGuid(reader.GetOrdinal("id"));
            var name = reader.GetString(reader.GetOrdinal("name"));
            var planId = reader.GetInt32(reader.GetOrdinal("plan_id"));
             var createdUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"));

var client  = new ApiClient
{
    Id  = id,
    Name = name,
    PlanId = planId,
    CreatedUtc = createdUtc




};
clients.Add(client);

}    
return clients;
    }


public async  Task ChangePlanAsync(Guid clientId, int planId)
    {
        await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync(); 
const string sql = @"UPDATE api_clients SET plan_id = @planid WHERE id = @clientid";

  await using var cmd = new SqlCommand(sql, conn);

cmd.Parameters.Add("@planid", SqlDbType.Int).Value = planId;
cmd.Parameters.Add("@clientid", SqlDbType.UniqueIdentifier).Value = clientId;

await cmd.ExecuteNonQueryAsync();

    }

public async  Task<bool> PlanExistsAsync(int plan)
    {
  await using var conn = new SqlConnection(_connectionString);
  
    await conn.OpenAsync();

    const string sql = @" SELECT 1 FROM plans WHERE id = @plan";

    await using var cmd = new SqlCommand(sql, conn);

    cmd.Parameters.Add("@plan", SqlDbType.Int).Value = plan;

    object? result = await cmd.ExecuteScalarAsync();

    return result is not null;
    }

}