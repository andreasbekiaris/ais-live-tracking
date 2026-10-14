
namespace InfraAis.Models;
public class VesselAnalyticsResponse
{
    public long Mmsi { get; set; }
    public AnalyticsWindow Window { get; set; } = new();
    public double? DistanceKm { get; set; }
    public double? DistanceExcludedKm { get; set; }
    public SpeedAnalytics Speed { get; set; } = new();
    public TimeAnalytics Time { get; set; } = new();
    public double? IdleHours { get; set; }
    public GapAnalytics Gaps { get; set; } = new();
    public ReportingAnalytics Reporting { get; set; } = new();
}

public class AnalyticsWindow
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public double CoveragePercent { get; set; }
}

public class SpeedAnalytics
{
    public double? AvgSogKnots { get; set; }
    public double? MaxSogKnots { get; set; }
    public double? MaxImpliedKnots { get; set; }
}

public class TimeAnalytics
{
    public double? UnderwayHours { get; set; }
    public double? StoppedHours { get; set; }
    public List<NavStatusTime> ByNavStatus { get; set; } = new();
}

public class NavStatusTime
{
    public int NavStatus { get; set; }
    public double? Hours { get; set; }
}

public class GapAnalytics
{
    public int Count { get; set; }
    public double TotalDarkHours { get; set; }
    public GapInfo? Longest { get; set; }         
}

public class GapInfo
{
    public double Hours { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public class ReportingAnalytics
{
    public int Messages { get; set; }
    public double MedianIntervalSeconds { get; set; }
    public DateTimeOffset? First { get; set; }     // null = 0 μηνύματα
    public DateTimeOffset? Last { get; set; }
}