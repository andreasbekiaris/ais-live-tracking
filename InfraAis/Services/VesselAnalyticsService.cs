
using InfraAis.Models;
using InfraAis.Utils;
using InfraAis.Repositories;
using Microsoft.Extensions.Options;
using InfraAis.Options;
namespace InfraAis.Services;

public class VesselAnalyticsService : IVesselAnalyticsService
{
    private readonly IAisRepository _repo;
    private readonly IOptions<AnalyticsOptions> _analyticsOptions;
    public VesselAnalyticsService(IAisRepository repo, IOptions<AnalyticsOptions> analyticsOptions)
    {
        _repo = repo;
        _analyticsOptions = analyticsOptions;
    }

    public async Task<VesselAnalyticsResponse?> GetVesselAnalytics(
        VesselAnalyticsValues analyticsValues)
    {
        List<VesselDataAnalytics> PositionData =
            await _repo.GetVesselAnalyticsAsync(analyticsValues);

        PositionData = PositionData
            .OrderBy(p => p.MsgTimestampUtc)
            .ToList();

        int i = PositionData.Count;

        double totalDistance = 0;
        double distanceExcluded = 0;
        double MaxImpliedSpeed = 0;

        double UnderwayHours = 0;
        double StoppedHours = 0;
        double IdleHours = 0;
        double currentStopHours = 0;

        int gapcount = 0;
        double totalDarkHours = 0;

        Dictionary<int, double> hoursByStatus = new();
        List<double> interval = new();

        GapInfo? longestgap = null;


        const double KmPerNauticalMile = 1.852;

        for (int j = 0; j < i - 1; j++)
        {
            var current = PositionData[j];
            var next = PositionData[j + 1];

            TimeSpan traveltime =
                next.MsgTimestampUtc - current.MsgTimestampUtc;

            double hours = traveltime.TotalHours;

            if (hours <= 0)
            {
                continue;
            }

            interval.Add(traveltime.TotalSeconds);

            double d = DistanceCalculator.CalculateDistance(
                current.Latitude,
                current.Longitude,
                next.Latitude,
                next.Longitude
            );

            if (hours > _analyticsOptions.Value.GapThresholdHours)
            {
                gapcount++;

                totalDarkHours += hours;
                distanceExcluded += d;

                if (longestgap == null ||
                    hours > longestgap.Hours)
                {
                    longestgap = new GapInfo
                    {
                        Hours = hours,
                        From = current.MsgTimestampUtc,
                        To = next.MsgTimestampUtc
                    };
                }

                if (currentStopHours > _analyticsOptions.Value.IdleThresholdHours)
                {
                    IdleHours += currentStopHours;
                }

                currentStopHours = 0;

                continue;
            }

            totalDistance += d;

            double impliedKnots =
                d / hours / KmPerNauticalMile;

            if (
                impliedKnots > MaxImpliedSpeed)
            {
                MaxImpliedSpeed = impliedKnots;
            }

            double? sog = (double?)next.Sog;

            if (sog == null)
            {
            }
            else if (sog.Value < _analyticsOptions.Value.StoppedSpeedThresholdKnots)
            {
                StoppedHours += hours;
                currentStopHours += hours;
            }
            else
            {
                UnderwayHours += hours;

                if (currentStopHours > _analyticsOptions.Value.IdleThresholdHours)
                {
                    IdleHours += currentStopHours;
                }

                currentStopHours = 0;
            }

            if (next.NavStatus is int navStatus)
            {
                if (!hoursByStatus.ContainsKey(navStatus))
                {
                    hoursByStatus[navStatus] = 0;
                }

                hoursByStatus[navStatus] += hours;
            }
        }

        if (currentStopHours > _analyticsOptions.Value.IdleThresholdHours)
        {
            IdleHours += currentStopHours;
        }

        List<NavStatusTime> NavStatusTimes =
            hoursByStatus.Select(x => new NavStatusTime
            {
                NavStatus = x.Key,
                Hours = x.Value
            }).ToList();

        interval.Sort();

        int g = interval.Count;
        double medianInterval = 0;

        if (g > 0)
        {
            if (g % 2 == 0)
            {
                medianInterval =
                    (interval[g / 2] + interval[g / 2 - 1]) / 2;
            }
            else
            {
                medianInterval = interval[g / 2];
            }
        }

        double SogSum = 0;
        double SogMax = 0;
        double AvgSog = 0;

        int n = 0;

        foreach (VesselDataAnalytics position in PositionData)
        {
            if (position.Sog is not null)
            {
                double sog = (double)position.Sog;

                SogSum += sog;
                n++;

                if (sog > SogMax)
                {
                    SogMax = sog;
                }
            }
        }

        if (n > 0)
        {
            AvgSog = SogSum / n;
        }





        var covaragePercent = 0.0;

        if (PositionData.Count >= 2)
        {
            TimeSpan timewindow = analyticsValues.To - analyticsValues.From;
            TimeSpan uncovered = PositionData.First().MsgTimestampUtc - analyticsValues.From;
            uncovered += analyticsValues.To - PositionData.Last().MsgTimestampUtc;
            var a = (uncovered.TotalHours + totalDarkHours) / timewindow.TotalHours * 100;
            covaragePercent = 100 - a;

        }

        return new VesselAnalyticsResponse
        {
            Mmsi = analyticsValues.Mmsi,

            Window = new AnalyticsWindow
            {
                From = analyticsValues.From,
                To = analyticsValues.To,
                CoveragePercent = covaragePercent
            },

            DistanceKm = totalDistance,
            DistanceExcludedKm = distanceExcluded,

            Speed = new SpeedAnalytics
            {
                AvgSogKnots = AvgSog,
                MaxSogKnots = SogMax,
                MaxImpliedKnots = MaxImpliedSpeed
            },

            Time = new TimeAnalytics
            {
                UnderwayHours = UnderwayHours,
                StoppedHours = StoppedHours,
                ByNavStatus = NavStatusTimes
            },

            IdleHours = IdleHours,

            Gaps = new GapAnalytics
            {
                Count = gapcount,
                TotalDarkHours = totalDarkHours,
                Longest = longestgap
            },

            Reporting = new ReportingAnalytics
            {
                Messages = PositionData.Count,
                MedianIntervalSeconds = medianInterval,

                First = PositionData
                    .FirstOrDefault()?.MsgTimestampUtc,

                Last = PositionData
                    .LastOrDefault()?.MsgTimestampUtc
            }
        };
    }
}
