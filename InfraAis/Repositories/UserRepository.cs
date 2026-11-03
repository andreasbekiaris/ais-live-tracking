
using Microsoft.Data.SqlClient;
using System.Data;
using InfraAis.Models.Auth;
using InfraAis.Options;
using Microsoft.Extensions.Options;

namespace InfraAis.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IOptions<AisDbOptions> options)
    {
        _connectionString = options.Value.AisDb;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
       await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

       const string sql = @"SELECT id,password_hash,role,created_utc FROM users WHERE username = @username " ;




        await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.Add("@username", SqlDbType.NVarChar, 64).Value = username; 

        await using var reader = await cmd.ExecuteReaderAsync();

         if (!await reader.ReadAsync())
        return null;

        return new User
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Username = username,
            PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
            Role = reader.GetString(reader.GetOrdinal("role")),
            CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))

        };
    }

    public async Task CreateAsync(User user)
    {
         await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();
    const string sql = @"INSERT INTO users (username, password_hash, role)
VALUES (@Username, @PasswordHash, @Role)";
await using var cmd = new SqlCommand(sql, conn);

cmd.Parameters.Add("@Username",SqlDbType.NVarChar, 64).Value = user.Username;
 cmd.Parameters.Add("@PasswordHash",SqlDbType.NVarChar,256).Value = user.PasswordHash;
 cmd.Parameters.Add("@Role",SqlDbType.NVarChar,16).Value = user.Role;

await cmd.ExecuteNonQueryAsync();

    }
}
