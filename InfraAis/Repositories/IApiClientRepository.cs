using InfraAis.Models.Auth;

namespace InfraAis.Repositories;

public interface IApiClientRepository
{
    Task<Guid> CreateAsync(ApiClient client);

    Task<ApiClient?> GetByIdAsync(Guid clientId);

    Task<List<ApiClient>> GetAllAsync();

    Task ChangePlanAsync(Guid clientId, int planId);
    Task<bool> PlanExistsAsync(int plan);
}