using InfraAis.Utils;
using InfraAis.Models;
using Xunit;

namespace InfraAis.Tests;

public class CursorCodecTests
{
    [Fact]
    public void Encode_Then_Decode_Returns_Same_Values()
    {
        var original = new PositionCursor
        {
            Timestamp = new DateTime(2006, 5, 29, 12, 30, 10, DateTimeKind.Utc),
            Id = 12345
        };

        var result = CursorCodec.Decode(CursorCodec.Encode(original));

        Assert.NotNull(result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Id, result.Id);
    }

    [Fact]
    public void Encode_Then_Decode_Keeps_Sub_Second_Precision()
    {
        
        var original = new PositionCursor
        {
            Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567),
            Id = 7
        };

        var result = CursorCodec.Decode(CursorCodec.Encode(original));

        Assert.Equal(original.Timestamp.Ticks, result!.Timestamp.Ticks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("trash")]
    [InlineData("abc")]
    [InlineData("gate|city")]
    public void Decode_Returns_Null_For_Invalid_Input(string input)
    {
        Assert.Null(CursorCodec.Decode(input));
    }

    [Fact]
    public void Decoded_Timestamp_Is_Utc()
    {
        var original = new PositionCursor
        {
            Timestamp = DateTime.UtcNow,
            Id = 1
        };

        var result = CursorCodec.Decode(CursorCodec.Encode(original));

        Assert.Equal(DateTimeKind.Utc, result!.Timestamp.Kind);
    }

    [Fact]
    public void Unspecified_Timestamp_From_Database_Is_Treated_As_Utc()
    {
        
        var fromDb = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Unspecified);

        var result = CursorCodec.Decode(CursorCodec.Encode(new PositionCursor { Timestamp = fromDb, Id = 5 }));

        Assert.Equal(DateTimeKind.Utc, result!.Timestamp.Kind);
        Assert.Equal(fromDb.Ticks, result.Timestamp.Ticks);
    }
}
