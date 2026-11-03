namespace InfraAis.Models.Auth;


public class Plans
{
    
public int Id { get; set; }
public string Name { get; set; } = "";
public int RateLimitPerMinute { get; set; }
public int? MonthlyQuota { get; set; }
public int? MaxHistoryDays { get; set; }
public bool AnalyticsEnabled { get; set; }




}