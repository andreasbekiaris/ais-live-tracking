using InfraAis.Models;

namespace InfraAis.Repositories;

public interface IAisRepository
{
    Task UpsertVesselAsync(VesselRecord vessel);
    Task InsertPositionAsync(PositionRecord position);
    Task InsertDeadLetterAsync(string rawPayload, string reason);


    Task GetMmsiByImoAsync(int imo);
}
