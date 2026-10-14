using InfraAis.Models;
namespace InfraAis.Services;


public interface IVesselAnalyticsService
{
    Task<VesselAnalyticsResponse?> GetVesselAnalytics(VesselAnalyticsValues analyticsValues);
}