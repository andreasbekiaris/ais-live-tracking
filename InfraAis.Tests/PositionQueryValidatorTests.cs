using Xunit;
using InfraAis.Models;
using InfraAis.Utils;
using InfraAis.Validation;

namespace InfraAis.Tests;

public class PositionQueryValidatorTests
{
    private readonly PositionQueryValidator _validator = new();

    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

   
    private static PositionQuery Fleet() => new() { From = From, To = To };

    [Fact]
    public void ValidFleetQuery_WithTimeWindow_IsValid()
    {
        var result = _validator.Validate(Fleet());

        Assert.True(result.IsValid);
        Assert.Equal(100, result.Value!.Limit);
        Assert.Equal(SortDirection.Desc, result.Value.Sort);
    }

    [Fact]
    public void ValidFleetQuery_WithOnlyBoundingBox_IsValid()
    {
        var query = new PositionQuery { MinLat = 50, MaxLat = 52, MinLon = 2, MaxLon = 4 };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value!.BoundingBox);
    }

    [Fact]
    public void InvertedTimeRange_IsRejected()
    {
        var result = _validator.Validate(new PositionQuery { From = To, To = From });

        Assert.False(result.IsValid);
        Assert.Contains("'from'", result.Error);
    }

    [Fact]
    public void Fleet_WithoutWindowOrBoundingBox_IsRejected()
    {
        var result = _validator.Validate(new PositionQuery());

        Assert.False(result.IsValid);
        Assert.Contains("time window", result.Error);
    }

    [Fact]
    public void Fleet_WithOnlyFrom_IsRejected()
    {
        var result = _validator.Validate(new PositionQuery { From = From });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SingleVessel_WithoutWindow_IsValid()
    {
        var result = _validator.Validate(new PositionQuery(), isFleet: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SingleVessel_WithOnlyFrom_IsValid()
    {
        var result = _validator.Validate(new PositionQuery { From = From }, isFleet: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SingleVessel_WithMmsiParameter_IsRejected()
    {
        var result = _validator.Validate(new PositionQuery { Mmsi = "255805678" }, isFleet: false);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(50.0, null, null, null)]
    [InlineData(50.0, 52.0, null, null)]
    [InlineData(50.0, 52.0, 2.0, null)]
    [InlineData(null, null, 2.0, 4.0)]
    public void PartialBoundingBox_IsRejected(double? minLat, double? maxLat, double? minLon, double? maxLon)
    {
        var query = Fleet();
        query.MinLat = minLat;
        query.MaxLat = maxLat;
        query.MinLon = minLon;
        query.MaxLon = maxLon;

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains("all four", result.Error);
    }

    [Fact]
    public void InvertedLatitude_IsRejected()
    {
        var query = Fleet();
        query.MinLat = 52; query.MaxLat = 50; query.MinLon = 2; query.MaxLon = 4;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void OutOfRangeLatitude_IsRejected()
    {
        var query = Fleet();
        query.MinLat = -95; query.MaxLat = 50; query.MinLon = 2; query.MaxLon = 4;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveLimit_IsRejected(int limit)
    {
        var query = Fleet();
        query.Limit = limit;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void LimitAboveCap_IsClampedTo1000()
    {
        var query = Fleet();
        query.Limit = 50_000;

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Equal(1000, result.Value!.Limit);
    }

    [Fact]
    public void InvertedSpeedBand_IsRejected()
    {
        var query = Fleet();
        query.MinSog = 20; query.MaxSog = 5;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0,x")]
    [InlineData("16")]
    [InlineData("-1")]
    public void InvalidNavStatus_IsRejected(string navStatus)
    {
        var query = Fleet();
        query.NavStatus = navStatus;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void NavStatusList_IsParsed()
    {
        var query = Fleet();
        query.NavStatus = "0, 1,5";

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { 0, 1, 5 }, result.Value!.NavStatuses);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("255805678,abc")]
    public void InvalidMmsiList_IsRejected(string mmsi)
    {
        var query = Fleet();
        query.Mmsi = mmsi;

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData("asc", SortDirection.Asc)]
    [InlineData("ASC", SortDirection.Asc)]
    [InlineData("desc", SortDirection.Desc)]
    [InlineData(null, SortDirection.Desc)]
    public void Sort_IsParsedCaseInsensitively(string? sort, SortDirection expected)
    {
        var query = Fleet();
        query.Sort = sort;

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!.Sort);
    }

    [Fact]
    public void InvalidSort_IsRejected()
    {
        var query = Fleet();
        query.Sort = "sideways";

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void GarbageCursor_IsRejected()
    {
        var query = Fleet();
        query.Cursor = "not-a-cursor";

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void ValidCursor_IsAccepted()
    {
        var query = Fleet();
        query.Cursor = CursorCodec.Encode(new PositionCursor { Timestamp = DateTime.UtcNow, Id = 42 });

        Assert.True(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void AllFiltersTogether_Compose()
    {
        var query = new PositionQuery
        {
            From = From, To = To,
            MinLat = 50, MaxLat = 52, MinLon = 2, MaxLon = 4,
            MinSog = 1, MaxSog = 20,
            NavStatus = "0,8",
            Mmsi = "255805678,244123456",
            Sort = "asc",
            Limit = 10
        };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
        var f = result.Value!;
        Assert.NotNull(f.BoundingBox);
        Assert.Equal(2, f.Mmsis.Count);
        Assert.Equal(2, f.NavStatuses.Count);
        Assert.Equal(1m, f.MinSog);
        Assert.Equal(20m, f.MaxSog);
        Assert.Equal(10, f.Limit);
        Assert.Equal(SortDirection.Asc, f.Sort);
    }
}
