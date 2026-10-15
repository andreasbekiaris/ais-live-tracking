using InfraAis.Models;
using InfraAis.Options;
using InfraAis.Repositories;
using InfraAis.Services;
using Moq;
using Xunit;

namespace InfraAis.Tests;

public class VesselAnalyticsServiceTests
{
    private const long Mmsi = 255805678;
    private const double Lon = 23.0;
    private const double KmPerHundredthDegree = 1.11195;

    private const double KmTolerance = 0.05;
    private const double KnotsTolerance = 0.05;
    private const double HoursTolerance = 1e-6;

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static AnalyticsOptions DefaultOptions() => new()
    {
        GapThresholdHours = 1.0,
        IdleThresholdHours = 0.5,
        StoppedSpeedThresholdKnots = 0.5
    };

    private static VesselDataAnalytics P(double minutes, double lat, double? sog, int? nav = 0) =>
        At(T0.AddMinutes(minutes), lat, sog, nav);

    private static VesselDataAnalytics At(DateTimeOffset ts, double lat, double? sog, int? nav = 0) => new()
    {
        Mmsi = Mmsi,
        Latitude = lat,
        Longitude = Lon,
        Sog = (decimal?)sog,
        NavStatus = nav,
        MsgTimestampUtc = ts
    };

    private static async Task<VesselAnalyticsResponse> Run(
        List<VesselDataAnalytics> track,
        DateTimeOffset from,
        DateTimeOffset to,
        AnalyticsOptions? options = null)
    {
        var repo = new Mock<IAisRepository>();
        repo.Setup(r => r.GetVesselAnalyticsAsync(It.IsAny<VesselAnalyticsValues>()))
            .ReturnsAsync(track);

        var service = new VesselAnalyticsService(
            repo.Object,
            Microsoft.Extensions.Options.Options.Create(options ?? DefaultOptions()));

        var result = await service.GetVesselAnalytics(
            new VesselAnalyticsValues { Mmsi = Mmsi, From = from, To = to });

        Assert.NotNull(result);
        return result!;
    }

    private static void Close(double expected, double? actual, double tolerance)
    {
        Assert.True(actual.HasValue, $"Expected {expected} but was null.");
        Assert.True(Math.Abs(expected - actual!.Value) <= tolerance,
            $"Expected {expected} ± {tolerance} but was {actual.Value}.");
    }

    private static double NavHours(VesselAnalyticsResponse r, int status) =>
        r.Time.ByNavStatus.Single(x => x.NavStatus == status).Hours ?? 0;

    [Fact]
    public async Task EmptyWindow_ReturnsZeroedResponse_NotNullOrError()
    {
        var from = T0;
        var to = T0.AddHours(4);

        var r = await Run(new List<VesselDataAnalytics>(), from, to);

        Assert.Equal(Mmsi, r.Mmsi);
        Assert.Equal(from, r.Window.From);
        Assert.Equal(to, r.Window.To);
        Close(0, r.Window.CoveragePercent, HoursTolerance);

        Close(0, r.DistanceKm, KmTolerance);
        Close(0, r.DistanceExcludedKm, KmTolerance);

        Close(0, r.Speed.AvgSogKnots, KnotsTolerance);
        Close(0, r.Speed.MaxSogKnots, KnotsTolerance);
        Close(0, r.Speed.MaxImpliedKnots, KnotsTolerance);

        Close(0, r.Time.UnderwayHours, HoursTolerance);
        Close(0, r.Time.StoppedHours, HoursTolerance);
        Assert.Empty(r.Time.ByNavStatus);
        Close(0, r.IdleHours, HoursTolerance);

        Assert.Equal(0, r.Gaps.Count);
        Close(0, r.Gaps.TotalDarkHours, HoursTolerance);
        Assert.Null(r.Gaps.Longest);

        Assert.Equal(0, r.Reporting.Messages);
        Close(0, r.Reporting.MedianIntervalSeconds, HoursTolerance);
        Assert.Null(r.Reporting.First);
        Assert.Null(r.Reporting.Last);
    }

    [Fact]
    public async Task SinglePoint_HasNoSegments_SoEverythingSegmentBasedIsZero()
    {
        var track = new List<VesselDataAnalytics> { P(0, 37.00, 12) };

        var r = await Run(track, T0, T0.AddHours(1));

        Close(0, r.DistanceKm, KmTolerance);
        Close(0, r.Speed.MaxImpliedKnots, KnotsTolerance);
        Close(0, r.Time.UnderwayHours, HoursTolerance);
        Close(0, r.Time.StoppedHours, HoursTolerance);
        Close(0, r.Reporting.MedianIntervalSeconds, HoursTolerance);
        Close(0, r.Window.CoveragePercent, HoursTolerance);

        Close(12, r.Speed.AvgSogKnots, KnotsTolerance);
        Close(12, r.Speed.MaxSogKnots, KnotsTolerance);

        Assert.Equal(1, r.Reporting.Messages);
        Assert.Equal(T0, r.Reporting.First);
        Assert.Equal(T0, r.Reporting.Last);
    }

    [Fact]
    public async Task TwoPoints_Moving_ComputesDistanceImpliedSpeedAndUnderway()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(30, 37.05, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(30));

        Close(5 * KmPerHundredthDegree, r.DistanceKm, KmTolerance);
        Close(0, r.DistanceExcludedKm, KmTolerance);
        Close(6.0, r.Speed.MaxImpliedKnots, KnotsTolerance);

        Close(0.5, r.Time.UnderwayHours, HoursTolerance);
        Close(0, r.Time.StoppedHours, HoursTolerance);
        Close(0.5, NavHours(r, 0), HoursTolerance);

        Close(1800, r.Reporting.MedianIntervalSeconds, HoursTolerance);
        Close(100, r.Window.CoveragePercent, 1e-6);
    }

    [Fact]
    public async Task Gap_IsExcludedFromDistanceAndTime_AndReported()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(10, 37.02, 12),
            P(190, 37.50, 12),
            P(200, 37.52, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(200));

        Close(4 * KmPerHundredthDegree, r.DistanceKm, KmTolerance);
        Close(48 * KmPerHundredthDegree, r.DistanceExcludedKm, KmTolerance);

        Assert.Equal(1, r.Gaps.Count);
        Close(3.0, r.Gaps.TotalDarkHours, HoursTolerance);
        Assert.NotNull(r.Gaps.Longest);
        Close(3.0, r.Gaps.Longest!.Hours, HoursTolerance);
        Assert.Equal(T0.AddMinutes(10), r.Gaps.Longest.From);
        Assert.Equal(T0.AddMinutes(190), r.Gaps.Longest.To);

        Close(20.0 / 60, r.Time.UnderwayHours, HoursTolerance);
        Close(20.0 / 60, NavHours(r, 0), HoursTolerance);

        Close(10, r.Window.CoveragePercent, 1e-6);

        Close(600, r.Reporting.MedianIntervalSeconds, HoursTolerance);
    }

    [Fact]
    public async Task TwoGaps_LongestIsTheBiggestOne()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(10, 37.02, 12),
            P(130, 37.10, 12),
            P(140, 37.12, 12),
            P(320, 37.30, 12),
            P(330, 37.32, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(330));

        Assert.Equal(2, r.Gaps.Count);
        Close(5.0, r.Gaps.TotalDarkHours, HoursTolerance);
        Close(3.0, r.Gaps.Longest!.Hours, HoursTolerance);
        Assert.Equal(T0.AddMinutes(140), r.Gaps.Longest.From);
        Assert.Equal(T0.AddMinutes(320), r.Gaps.Longest.To);
    }

    [Fact]
    public async Task AllStopped_TwoHours_IsStoppedAndIdle()
    {
        var track = Enumerable.Range(0, 13)
            .Select(i => P(i * 10, 37.00, 0.1, nav: 1))
            .ToList();

        var r = await Run(track, T0, T0.AddHours(2));

        Close(2.0, r.Time.StoppedHours, HoursTolerance);
        Close(0, r.Time.UnderwayHours, HoursTolerance);
        Close(2.0, r.IdleHours, HoursTolerance);
        Close(2.0, NavHours(r, 1), HoursTolerance);

        Close(0, r.DistanceKm, KmTolerance);
        Close(0, r.Speed.MaxImpliedKnots, KnotsTolerance);
        Close(0.1, r.Speed.AvgSogKnots, KnotsTolerance);
    }

    [Fact]
    public async Task ShortStop_UnderIdleThreshold_IsStoppedButNotIdle()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(10, 37.02, 0.2),
            P(20, 37.02, 0.2),
            P(30, 37.04, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(30));

        Close(20.0 / 60, r.Time.StoppedHours, HoursTolerance);
        Close(10.0 / 60, r.Time.UnderwayHours, HoursTolerance);
        Close(0, r.IdleHours, HoursTolerance);
    }

    [Fact]
    public async Task LongStop_IsClosedAsIdle_WhenVesselStartsMoving()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 0.1),
            P(10, 37.00, 0.1),
            P(20, 37.00, 0.1),
            P(30, 37.00, 0.1),
            P(40, 37.00, 0.1),
            P(50, 37.02, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(50));

        Close(40.0 / 60, r.Time.StoppedHours, HoursTolerance);
        Close(40.0 / 60, r.IdleHours, HoursTolerance);
        Close(10.0 / 60, r.Time.UnderwayHours, HoursTolerance);
    }

    [Fact]
    public async Task Gap_ClosesTheCurrentStop()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 0.1),
            P(10, 37.00, 0.1),
            P(20, 37.00, 0.1),
            P(30, 37.00, 0.1),
            P(40, 37.00, 0.1),
            P(160, 37.00, 0.1),
            P(170, 37.00, 0.1),
            P(180, 37.00, 0.1)
        };

        var r = await Run(track, T0, T0.AddMinutes(180));

        Assert.Equal(1, r.Gaps.Count);
        Close(2.0, r.Gaps.TotalDarkHours, HoursTolerance);
        Close(1.0, r.Time.StoppedHours, HoursTolerance);
        Close(40.0 / 60, r.IdleHours, HoursTolerance);
    }

    [Fact]
    public async Task NullSog_InTheMiddleOfAStop_DoesNotBreakTheIdleRun()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 0.1),
            P(10, 37.00, 0.1),
            P(20, 37.00, 0.1),
            P(25, 37.00, null),
            P(35, 37.00, 0.1),
            P(45, 37.00, 0.1)
        };

        var r = await Run(track, T0, T0.AddMinutes(45));

        Close(40.0 / 60, r.Time.StoppedHours, HoursTolerance);
        Close(40.0 / 60, r.IdleHours, HoursTolerance);
        Close(0, r.Time.UnderwayHours, HoursTolerance);
    }

    [Fact]
    public async Task AvgSog_IgnoresNullSpeeds()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(10, 37.02, 12),
            P(20, 37.04, null),
            P(30, 37.06, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(30));

        Close(12, r.Speed.AvgSogKnots, KnotsTolerance);
        Close(12, r.Speed.MaxSogKnots, KnotsTolerance);
    }

    [Fact]
    public async Task AllSogNull_SpeedsAreZero_NotNull()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, null),
            P(10, 37.02, null)
        };

        var r = await Run(track, T0, T0.AddMinutes(10));

        Close(0, r.Speed.AvgSogKnots, KnotsTolerance);
        Close(0, r.Speed.MaxSogKnots, KnotsTolerance);
    }

    [Fact]
    public async Task DuplicateTimestamps_AreSkipped_NoInfinityOrNaN()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12),
            P(0, 37.01, 12),
            P(10, 37.03, 12)
        };

        var r = await Run(track, T0, T0.AddMinutes(10));

        Assert.False(double.IsInfinity(r.Speed.MaxImpliedKnots ?? 0));
        Assert.False(double.IsNaN(r.Speed.MaxImpliedKnots ?? 0));

        Close(7.2, r.Speed.MaxImpliedKnots, KnotsTolerance);
        Close(600, r.Reporting.MedianIntervalSeconds, HoursTolerance);
        Assert.Equal(3, r.Reporting.Messages);
    }

    [Fact]
    public async Task ByNavStatus_HasOneEntryPerStatus_WithSummedHours()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 12, nav: 0),
            P(10, 37.02, 12, nav: 0),
            P(20, 37.02, 0.1, nav: 1),
            P(30, 37.02, 0.1, nav: 1),
            P(40, 37.02, 0.1, nav: 5)
        };

        var r = await Run(track, T0, T0.AddMinutes(40));

        Assert.Equal(3, r.Time.ByNavStatus.Count);
        Close(10.0 / 60, NavHours(r, 0), HoursTolerance);
        Close(20.0 / 60, NavHours(r, 1), HoursTolerance);
        Close(10.0 / 60, NavHours(r, 5), HoursTolerance);
    }

    [Fact]
    public async Task Coverage_CountsSilenceBeforeFirstAndAfterLastMessage()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(60, 37.00, 12),
            P(90, 37.05, 12),
            P(120, 37.10, 12),
            P(150, 37.15, 12),
            P(180, 37.20, 12)
        };

        var r = await Run(track, T0, T0.AddHours(4));

        Close(50, r.Window.CoveragePercent, 1e-6);
    }

    [Fact]
    public async Task MedianInterval_EvenNumberOfIntervals_IsAverageOfTheMiddleTwo()
    {
        var track = new List<VesselDataAnalytics>
        {
            At(T0, 37.00, 12),
            At(T0.AddSeconds(10), 37.00, 12),
            At(T0.AddSeconds(30), 37.00, 12),
            At(T0.AddSeconds(60), 37.00, 12),
            At(T0.AddSeconds(100), 37.00, 12)
        };

        var r = await Run(track, T0, T0.AddSeconds(100));

        Close(25, r.Reporting.MedianIntervalSeconds, HoursTolerance);
    }

    [Fact]
    public async Task Thresholds_ComeFromOptions_NotHardcoded()
    {
        var track = new List<VesselDataAnalytics>
        {
            P(0, 37.00, 1.0),
            P(180, 37.00, 1.0)
        };

        var options = new AnalyticsOptions
        {
            GapThresholdHours = 4.0,
            IdleThresholdHours = 0.5,
            StoppedSpeedThresholdKnots = 2.0
        };

        var r = await Run(track, T0, T0.AddHours(3), options);

        Assert.Equal(0, r.Gaps.Count);
        Close(3.0, r.Time.StoppedHours, HoursTolerance);
        Close(3.0, r.IdleHours, HoursTolerance);
    }
}



