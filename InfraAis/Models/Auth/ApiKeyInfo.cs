namespace InfraAis.Models.Auth;


public class ApiKeyInfo
{
     public Guid KeyId { get; set; }
    public Guid ClientId { get; set; }
    public int PlanId { get; set; }
    public int RateLimitPerMinute { get; set; }
    public int? MonthlyQuota { get; set; }
    public int? MaxHistoryDays { get; set; }
    public bool AnalyticsEnabled { get; set; }
}
