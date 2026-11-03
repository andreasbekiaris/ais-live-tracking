namespace InfraAis.Models.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }
}