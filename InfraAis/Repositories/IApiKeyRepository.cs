using InfraAis.Models.Auth;
namespace InfraAis.Repositories;


public interface IApiKeyRepository
{
    Task<ApiKeyInfo?> GetActiveByHashAsync(byte[] hash);

    Task CreateKeyAsync(Guid clientId,GeneratedApiKey key);

   Task<bool> RevokeKeyAsync(Guid keyId);
    Task<List<ApiKeySummary>> ListByClientAsync(Guid clientId);
}