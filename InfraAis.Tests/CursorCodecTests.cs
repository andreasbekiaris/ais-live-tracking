using InfraAis.Utils;
using InfraAis.Models;
using Xunit;

public class CursorCodecTests
{
    private readonly CursorCodec _codec = new();

    [Fact]
    public void Encode_Then_Decode_Returns_Same_Values()
    {
        var original = new PositionCursor
        {
            Timestamp = new DateTime(2006, 5, 29, 12, 30, 10, DateTimeKind.Utc),
            Id = 12345
        };

        var result = _codec.Decode(_codec.Encode(original));

        Assert.NotNull(result);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Id, result.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("trash")]
    [InlineData("abc")]              
    [InlineData("gate|city")]        
    public void Decode_Returns_Null_For_Invalid_Input(string input)
    {
        Assert.Null(_codec.Decode(input));
    }

    [Fact]
    public void Decoded_Timestamp_Is_Utc()
    {
        var original = new PositionCursor
        {
            Timestamp = DateTime.UtcNow,
            Id = 1
        };

        var result = _codec.Decode(_codec.Encode(original));

        Assert.Equal(DateTimeKind.Utc, result!.Timestamp.Kind);
    }
}