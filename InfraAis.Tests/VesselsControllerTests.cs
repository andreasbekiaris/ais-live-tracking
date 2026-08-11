using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InfraAis.Controllers;
using InfraAis.Models;
using InfraAis.Repositories;
using InfraAis.Services;

namespace InfraAis.Tests;

public class VesselsControllerTests
{
    private static (VesselsController ctrl, Mock<IVesselIdentifierResolver> resolver, Mock<IAisRepository> repo) Build()
    {
        var resolver = new Mock<IVesselIdentifierResolver>();
        var repo = new Mock<IAisRepository>();
        var ctrl = new VesselsController(resolver.Object, repo.Object,
            new Mock<ILogger<VesselsController>>().Object);
        return (ctrl, resolver, repo);
    }

    [Fact]
    public async Task GetLatest_MalformedIdentifier_Returns400()
    {
        var (ctrl, resolver, repo) = Build();
        resolver.Setup(r => r.Parse("bad", null)).Returns((VesselIdentifier?)null);

        var result = await ctrl.GetLatestPosition("bad", null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        repo.Verify(r => r.GetLatestPositionAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task GetLatest_UnknownImo_Returns404()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9999999 };
        resolver.Setup(r => r.Parse("9999999", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync((long?)null);

        var result = await ctrl.GetLatestPosition("9999999", null);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        repo.Verify(r => r.GetLatestPositionAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task GetLatest_KnownVesselNoPositions_Returns404()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        resolver.Setup(r => r.Parse("255805678", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetLatestPositionAsync(255805678)).ReturnsAsync((LatestPositionRecord?)null);
        repo.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord { Mmsi = 255805678 });

        var result = await ctrl.GetLatestPosition("255805678", null);

        var body = Assert.IsType<NotFoundObjectResult>(result.Result).Value!.ToString();
        Assert.Contains("no stored positions", body);
    }

    [Fact]
    public async Task GetLatest_UnknownMmsi_Returns404_SayingVesselIsUnknown()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 999999999 };
        resolver.Setup(r => r.Parse("999999999", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(999999999);
        repo.Setup(r => r.GetLatestPositionAsync(999999999)).ReturnsAsync((LatestPositionRecord?)null);
        repo.Setup(r => r.GetVesselAsync(999999999)).ReturnsAsync((VesselRecord?)null);

        var result = await ctrl.GetLatestPosition("999999999", null);

        var body = Assert.IsType<NotFoundObjectResult>(result.Result).Value!.ToString();
        Assert.Contains("No vessel known", body);
        Assert.DoesNotContain("no stored positions", body);
    }

    [Fact]
    public async Task GetLatest_Success_Returns200_WithMappedFields()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        resolver.Setup(r => r.Parse("255805678", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetLatestPositionAsync(255805678)).ReturnsAsync(new LatestPositionRecord
        {
            Mmsi = 255805678,
            Name = "BUXCLIFF",
            Latitude = 51.4,
            Longitude = 3.1,
            NavStatus = 0,
            MsgTimestampUtc = DateTime.UtcNow.AddSeconds(-90)
        });

        var result = await ctrl.GetLatestPosition("255805678", null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LatestPositionResponse>(ok.Value);
        Assert.Equal("Under way using engine", body.NavStatusText);
        Assert.InRange(body.AgeSeconds, 85, 95);
    }

    [Fact]
    public async Task GetLatest_TwoNotFoundCases_HaveDifferentMessages()
    {
        var (ctrlA, resolverA, _) = Build();
        var imoId = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9999999 };
        resolverA.Setup(r => r.Parse("9999999", null)).Returns(imoId);
        resolverA.Setup(r => r.ResolveToMmsiAsync(imoId)).ReturnsAsync((long?)null);
        var resultA = await ctrlA.GetLatestPosition("9999999", null);
        var bodyA = Assert.IsType<NotFoundObjectResult>(resultA.Result).Value!.ToString();

        var (ctrlB, resolverB, repoB) = Build();
        var mmsiId = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        resolverB.Setup(r => r.Parse("255805678", null)).Returns(mmsiId);
        resolverB.Setup(r => r.ResolveToMmsiAsync(mmsiId)).ReturnsAsync(255805678);
        repoB.Setup(r => r.GetLatestPositionAsync(255805678)).ReturnsAsync((LatestPositionRecord?)null);
        repoB.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord { Mmsi = 255805678 });
        var resultB = await ctrlB.GetLatestPosition("255805678", null);
        var bodyB = Assert.IsType<NotFoundObjectResult>(resultB.Result).Value!.ToString();

        Assert.NotEqual(bodyA, bodyB);
    }

    [Fact]
    public async Task GetVesselInfo_Success_ReturnsFullStaticAndVoyageData()
    {
        var (ctrl, resolver, repo) = Build();
        var parsed = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };
        var eta = new DateTime(2024, 5, 22, 6, 0, 0, DateTimeKind.Utc);
        resolver.Setup(r => r.Parse("255805678", null)).Returns(parsed);
        resolver.Setup(r => r.ResolveToMmsiAsync(parsed)).ReturnsAsync(255805678);
        repo.Setup(r => r.GetVesselAsync(255805678)).ReturnsAsync(new VesselRecord
        {
            Mmsi = 255805678,
            Imo = 9074729,
            Name = "BUXCLIFF",
            CallSign = "CQAD",
            ShipType = 70,
            DimToBow = 150,
            DimToStern = 57,
            DimToPort = 16,
            DimToStarboard = 16,
            Draught = 11.5m,
            Destination = "ROTTERDAM",
            Eta = eta,
            FirstSeenUtc = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeenUtc = new DateTime(2024, 5, 20, 9, 21, 31, DateTimeKind.Utc)
        });

        var result = await ctrl.GetVesselInfo("255805678", null);

        var body = Assert.IsType<VesselInfoResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((short)150, body.DimToBow);
        Assert.Equal((short)57, body.DimToStern);
        Assert.Equal((short)16, body.DimToPort);
        Assert.Equal((short)16, body.DimToStarboard);
        Assert.Equal(11.5m, body.Draught);
        Assert.Equal("ROTTERDAM", body.Destination);
        Assert.Equal(eta, body.Eta);
    }

    [Theory]
    [InlineData(0, "Under way using engine")]
    [InlineData(1, "At anchor")]
    [InlineData(5, "Moored")]
    [InlineData(null, "Unknown")]
    [InlineData(11, "Reserved")]
    public void NavStatusMapper_MapsCodesToText(int? code, string expected)
    {
        Assert.Equal(expected, InfraAis.Utils.NavStatusMapper.ToText(code));
    }
}
