namespace InfraAis.Models.Auth;

public class User
{
    

public Guid Id { get; init; }
public string Username { get; set; } = "";
public string PasswordHash { get; set; } = "";
public string Role { get; set; } = "";

public DateTime CreatedUtc {get; set;}


}