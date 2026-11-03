
namespace InfraAis.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKeyBase64 { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 30;
}
