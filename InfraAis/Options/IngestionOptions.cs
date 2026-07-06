namespace InfraAis.Options;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public int FutureSkewMinutes { get; set; } = 5;
    public int ReconnectBaseDelaySeconds { get; set; } = 1;
    public int ReconnectMaxDelaySeconds { get; set; } = 30;
    public int SummaryIntervalSeconds { get; set; } = 30;
}