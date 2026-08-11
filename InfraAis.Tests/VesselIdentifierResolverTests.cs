using Xunit;
using Moq;
using InfraAis.Models;
using InfraAis.Repositories;
using InfraAis.Services;

namespace InfraAis.Tests;

public class VesselIdentifierResolverTests
{
    private static VesselIdentifierResolver Build(Mock<IAisRepository>? mock = null)
        => new(mock?.Object ?? new Mock<IAisRepository>().Object);

    [Fact]
    public void Parse_NineDigits_IsMmsi()
    {
        var result = Build().Parse("255805678", null);

        Assert.NotNull(result);
        Assert.Equal(IdentifierType.Mmsi, result!.Type);
        Assert.Equal(255805678, result.Value);
    }

    [Fact]
    public void Parse_SevenDigits_IsImo()
    {
        var result = Build().Parse("9074729", null);

        Assert.NotNull(result);
        Assert.Equal(IdentifierType.Imo, result!.Type);
        Assert.Equal(9074729, result.Value);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("abc1234")]
    [InlineData("")]
    [InlineData("-9074729")]
    public void Parse_MalformedInput_ReturnsNull(string raw)
    {
        Assert.Null(Build().Parse(raw, null));
    }

    [Fact]
    public void Parse_OverrideMatchingLength_Works()
    {
        var result = Build().Parse("9074729", "imo");

        Assert.NotNull(result);
        Assert.Equal(IdentifierType.Imo, result!.Type);
    }

    [Theory]
    [InlineData("255805678", "imo")]
    [InlineData("9074729", "mmsi")]
    [InlineData("9074729", "banana")]
    public void Parse_OverrideConflict_ReturnsNull(string raw, string idType)
    {
        Assert.Null(Build().Parse(raw, idType));
    }

    [Fact]
    public void Parse_OverrideIsCaseInsensitive()
    {
        var result = Build().Parse("9074729", "IMO");

        Assert.NotNull(result);
        Assert.Equal(IdentifierType.Imo, result!.Type);
    }

    [Fact]
    public async Task Resolve_Mmsi_ReturnsValueWithoutDbLookup()
    {
        var mock = new Mock<IAisRepository>();
        var resolver = Build(mock);
        var id = new VesselIdentifier { Type = IdentifierType.Mmsi, Value = 255805678 };

        var mmsi = await resolver.ResolveToMmsiAsync(id);

        Assert.Equal(255805678, mmsi);
        mock.Verify(r => r.GetMmsiByImoAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_KnownImo_ReturnsMappedMmsi()
    {
        var mock = new Mock<IAisRepository>();
        mock.Setup(r => r.GetMmsiByImoAsync(9074729)).ReturnsAsync(255805678);
        var id = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9074729 };

        var mmsi = await Build(mock).ResolveToMmsiAsync(id);

        Assert.Equal(255805678, mmsi);
    }

    [Fact]
    public async Task Resolve_UnknownImo_ReturnsNull()
    {
        var mock = new Mock<IAisRepository>();
        mock.Setup(r => r.GetMmsiByImoAsync(It.IsAny<int>())).ReturnsAsync((long?)null);
        var id = new VesselIdentifier { Type = IdentifierType.Imo, Value = 9999999 };

        Assert.Null(await Build(mock).ResolveToMmsiAsync(id));
    }
}
