using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InfraAis.Controllers;
using InfraAis.Models;
using InfraAis.Repositories;
using InfraAis.Services;
using InfraAis.Validation;

namespace InfraAis.Tests;

public class PositionsControllerTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    private static (PositionsController ctrl, Mock<IvesselIdentifierResolver> resolver, Mock<IAisRepository> repo) Build()
    {
        var resolver = new Mock<IvesselIdentifierResolver>();
        var repo = new Mock<IAisRepository>();
        var ctrl = new PositionsController(repo.Object,
            new Mock<ILogger<PositionsController>>().Object,
            resolver.Object,
            new PositionQueryValidator());
        return (ctrl, resolver, repo);
    }

    private static Task<ActionResult<PagedResponse>> Fleet(PositionsController ctrl,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        double? minLat = null, double? maxLat = null, double? minLon = null, double? maxLon = null,
        int? limit = null)
        => ctrl.GetPositions(from, to, null, null, null, null, null, limit, null, minLat, maxLat, minLon, maxLon);

    private static Task<ActionResult<PagedResponse>> Vessel(PositionsController ctrl, string identifier,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
        => ctrl.GetPositionsByMmsiAsync(identifier, null, from, to, null, null, null, null, null, null, null, null, null, null);

    

    [Fact]
    public async Task Fleet_ValidWindow_Returns200WithPage()
    {
        var (ctrl, _, repo) = Build();
        var page = new PagedResponse { Items = new List<PositionHistoryRecord>(), Count = 0 };
        repo.Setup(r => r.GetPositionsAsync(It.IsAny<PositionQueryFilters>())).ReturnsAsync(page);

        var result = await Fleet(ctrl, From, To);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Fleet_NoWindowNoBox_Returns400_AndDoesNotHitDatabase()
    {
        var (ctrl, _, repo) = Build();

        var result = await Fleet(ctrl);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        repo.Verify(r => r.GetPositionsAsync(It.IsAny<PositionQueryFilters>()), Times.Never);
    }

    [Fact]
    public async Task Fleet_InvertedRange_Returns400()
    {
        var (ctrl, _, _) = Build();

        var result = await Fleet(ctrl, To, From);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Fleet_PartialBoundingBox_Returns400()
    {
        var (ctrl, _, _) = Build();

        var result = await Fleet(ctrl, From, To, minLat: 50, maxLat: 52);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Fleet_LimitAboveCap_IsClampedBeforeReachingRepository()
    {
        var (ctrl, _, repo) = Build();
        repo.Setup(r => r.GetPositionsAsync(It.IsAny<PositionQueryFilters>())).ReturnsAsync(new PagedResponse());

        await Fleet(ctrl, From, To, limit: 50_000);

        repo.Verify(r => r.GetPositionsAsync(It.Is<PositionQueryFilters>(f => f.Limit == 1000)), Times.Once);
    }

    

    [Fact]
    public async Task Vessel_MalformedIdentifier_Returns400()
    {
        var (ctrl, resolver, repo) = Build();
        resolver.Setup(r => r.Parse("bad", null)).Returns((VesselIdentifier?)null);

        var result = await Vessel(ctrl, "bad");

        Assert.IsType<BadRequestObjectResult>(result.Result);
        repo.Verify(r => r.GetPositionsByMmsiAsync(It.IsAny<long>(), It.IsAny<PositionQueryFilters>()), Times.Never);
    }

    [Fact]
    public async Task Vessel_UnknownImo_Returns404()
    {
        var (ctrl, resolver, _) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9999999 };
        resolver.Setup(r => r.Parse("9999999", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync((long?)null);

        var result = await Vessel(ctrl, "9999999");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Vessel_UnknownMmsi_Returns404()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 999999999 };
        resolver.Setup(r => r.Parse("999999999", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(999999999);
        repo.Setup(r => r.GetVesselAsync(999999999)).ReturnsAsync((VesselRecord?)null);

        var result = await Vessel(ctrl, "999999999");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Vessel_KnownImo_QueriesByResolvedMmsi_NotByImo()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9074729 };
        resolver.Setup(r => r.Parse("9074729", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord { Mmsi = 255805678 });
        repo.Setup(r => r.GetPositionsByMmsiAsync(255805678, It.IsAny<PositionQueryFilters>()))
            .ReturnsAsync(new PagedResponse());

        var result = await Vessel(ctrl, "9074729");

        Assert.IsType<OkObjectResult>(result.Result);
        repo.Verify(r => r.GetPositionsByMmsiAsync(255805678, It.IsAny<PositionQueryFilters>()), Times.Once);
    }

    [Fact]
    public async Task Vessel_NoTimeWindow_IsAllowed()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        resolver.Setup(r => r.Parse("255805678", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord { Mmsi = 255805678 });
        repo.Setup(r => r.GetPositionsByMmsiAsync(255805678, It.IsAny<PositionQueryFilters>()))
            .ReturnsAsync(new PagedResponse());

        var result = await Vessel(ctrl, "255805678");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Vessel_InvertedRange_Returns400()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        resolver.Setup(r => r.Parse("255805678", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord { Mmsi = 255805678 });

        var result = await Vessel(ctrl, "255805678", To, From);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
