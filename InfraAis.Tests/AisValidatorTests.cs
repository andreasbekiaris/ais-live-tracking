using Xunit;
using InfraAis.Validation;

namespace InfraAis.Tests;

public class AisValidatorTests
{
    [Theory]
    [InlineData(255805678)]
    [InlineData(100000000)]
    [InlineData(999999999)]
    public void IsValidMmsi_ReturnsTrue_ForNineDigits(long mmsi)
    {
        Assert.True(AisValidator.IsValidMmsi(mmsi));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-255805678)]
    [InlineData(12345)]
    [InlineData(99999999)]
    [InlineData(1000000000)]
    public void IsValidMmsi_ReturnsFalse_ForWrongLength(long mmsi)
    {
        Assert.False(AisValidator.IsValidMmsi(mmsi));
    }

    [Theory]
    [InlineData(51.4, 3.1)]
    [InlineData(0, 0)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void IsValidPosition_ReturnsTrue_ForValidCoords(double lat, double lon)
    {
        Assert.True(AisValidator.IsValidPosition(lat, lon));
    }

    [Theory]
    [InlineData(91, 181)]
    [InlineData(91, 3.1)]
    [InlineData(51.4, 181)]
    [InlineData(-91, 0)]
    [InlineData(0, -181)]
    public void IsValidPosition_ReturnsFalse_ForOutOfRange(double lat, double lon)
    {
        Assert.False(AisValidator.IsValidPosition(lat, lon));
    }

    [Fact]
    public void NormalizeSog_ReturnsNull_ForSentinel()
    {
        Assert.Null(AisValidator.NormalizeSog(102.3));
    }

    [Fact]
    public void NormalizeSog_ReturnsValue_ForRealSpeed()
    {
        Assert.Equal(15.6m, AisValidator.NormalizeSog(15.6));
    }

    [Fact]
    public void NormalizeCog_ReturnsNull_ForSentinel()
    {
        Assert.Null(AisValidator.NormalizeCog(360));
    }

    [Fact]
    public void NormalizeCog_ReturnsValue_ForRealCourse()
    {
        Assert.Equal(270.2m, AisValidator.NormalizeCog(270.2));
    }

    [Fact]
    public void NormalizeHeading_ReturnsNull_ForSentinel()
    {
        Assert.Null(AisValidator.NormalizeHeading(511));
    }

    [Fact]
    public void NormalizeHeading_ReturnsValue_ForRealHeading()
    {
        Assert.Equal((short)271, AisValidator.NormalizeHeading(271));
    }

    [Fact]
    public void TryParseTimestamp_ParsesGoStyleFormat()
    {
        var ok = AisValidator.TryParseTimestamp("2024-05-20 09:21:31.781972101 +0000 UTC", out var ts);

        Assert.True(ok);
        Assert.Equal(2024, ts.Year);
        Assert.Equal(5, ts.Month);
        Assert.Equal(20, ts.Day);
        Assert.Equal(9, ts.Hour);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("2024-13-45 99:99:99")]
    public void TryParseTimestamp_ReturnsFalse_ForBadInput(string raw)
    {
        var ok = AisValidator.TryParseTimestamp(raw, out _);
        Assert.False(ok);
    }

    [Fact]
    public void IsNotFutureSkewed_ReturnsTrue_ForPastTimestamp()
    {
        var past = DateTime.UtcNow.AddHours(-1);
        Assert.True(AisValidator.IsNotFutureSkewed(past, 5));
    }

    [Fact]
    public void IsNotFutureSkewed_ReturnsFalse_ForFarFutureTimestamp()
    {
        var future = DateTime.UtcNow.AddHours(1);
        Assert.False(AisValidator.IsNotFutureSkewed(future, 5));
    }

    [Fact]
    public void IsNotFutureSkewed_ReturnsTrue_WithinSkewTolerance()
    {
        var slightlyAhead = DateTime.UtcNow.AddMinutes(2);
        Assert.True(AisValidator.IsNotFutureSkewed(slightlyAhead, 5));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    public void NormalizeNavStatus_ReturnsValue_ForValidCodes(int code)
    {
        Assert.Equal(code, AisValidator.NormalizeNavStatus(code));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(200)]
    public void NormalizeNavStatus_ReturnsNull_ForInvalidCodes(int code)
    {
        Assert.Null(AisValidator.NormalizeNavStatus(code));
    }


    [Fact]
    public void NormalizeRateOfTurn_ReturnsNull_ForSentinel()
    {
        Assert.Null(AisValidator.NormalizeRateOfTurn(-128));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-127)]
    [InlineData(127)]
    public void NormalizeRateOfTurn_ReturnsValue_ForValidRange(int rot)
    {
        Assert.Equal((short)rot, AisValidator.NormalizeRateOfTurn(rot));
    }
}