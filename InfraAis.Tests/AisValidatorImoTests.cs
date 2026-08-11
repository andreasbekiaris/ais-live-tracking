using Xunit;
using InfraAis.Validation;

namespace InfraAis.Tests;

public class AisValidatorImoTests
{
    [Theory]
    [InlineData(9074729)]
    [InlineData(9704611)]
    [InlineData(9321483)]
    public void IsValidImo_ReturnsTrue_ForValidChecksums(int imo)
    {
        Assert.True(AisValidator.IsValidImo(imo));
    }

    [Theory]
    [InlineData(9074728)]
    [InlineData(9074720)]
    [InlineData(9704610)]
    public void IsValidImo_ReturnsFalse_ForBadChecksums(int imo)
    {
        Assert.False(AisValidator.IsValidImo(imo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(123)]
    [InlineData(999999)]
    [InlineData(10000000)]
    [InlineData(-9074729)]
    public void IsValidImo_ReturnsFalse_ForWrongLength(int imo)
    {
        Assert.False(AisValidator.IsValidImo(imo));
    }
}
