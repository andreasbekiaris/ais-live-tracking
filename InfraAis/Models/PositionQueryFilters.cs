
namespace InfraAis.Models;

public class PositionQueryFilters
{
   
    public IReadOnlyList<long> Mmsis { get; init; } = new List<long>();
    public IReadOnlyList<int> NavStatuses { get; init; } = new List<int>();
    public SortDirection Sort { get; init; }
    public int Limit { get; init; }

    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public decimal? MinSog { get; init; }
    public decimal? MaxSog { get; init; }

    public PositionCursor? Cursor { get; init; }

public BoundingBox? BoundingBox { get; init; }
}