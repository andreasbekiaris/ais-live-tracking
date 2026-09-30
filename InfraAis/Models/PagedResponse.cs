namespace InfraAis.Models;
public class PagedResponse
{
    public List<PositionHistoryRecord>? Items { get; set; }
    public string? NextCursor { get; set; }
    public int Count { get; set; }
}