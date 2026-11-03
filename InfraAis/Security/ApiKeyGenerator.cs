using System.Security.Cryptography;
using System.Text;

namespace InfraAis.Security;




public static class ApiKeyGenerator
{

public static GeneratedApiKey GenerateApiKey()
    {
        
        
   byte[] bytes = RandomNumberGenerator.GetBytes(32);
string key = Convert.ToHexString(bytes);
string rawKey = "ais_"+ key;
byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)
);
string extract = rawKey[..8];

return new GeneratedApiKey(rawKey, hash, extract);
    }


}