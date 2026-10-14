namespace InfraAis.Options;


public class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public double GapThresholdHours { get; set; } = 1.0;
    public double IdleThresholdHours { get; set; } = 0.5;
    public double StoppedSpeedThresholdKnots { get; set; } = 0.5;

}