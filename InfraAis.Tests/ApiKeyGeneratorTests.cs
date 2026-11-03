
using System.Security.Cryptography;
using System.Text;
using InfraAis.Security;
using Xunit;

namespace InfraAis.Tests;

public class ApiKeyGeneratorTests
{
    [Fact]
    public void GenerateApiKey_ReturnsValidHashAndPrefix()
    {
        var key = ApiKeyGenerator.GenerateApiKey();

        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key.RawKey));

        Assert.StartsWith("ais_", key.RawKey);
        Assert.Equal(32, key.Hash.Length);
        Assert.Equal(expectedHash, key.Hash);
        Assert.Equal(key.RawKey[..8], key.Prefix);
    }

    [Fact]
    public void GenerateApiKey_TwoCallsProduceDifferentKeys()
    {
        var first = ApiKeyGenerator.GenerateApiKey();
        var second = ApiKeyGenerator.GenerateApiKey();

        Assert.NotEqual(first.RawKey, second.RawKey);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
