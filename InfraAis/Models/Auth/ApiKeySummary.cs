
namespace InfraAis.Models.Auth;




public class ApiKeySummary
{
    public Guid KeyId { get; set; }
    public Guid ClientId { get; set; }
    public string KeyPrefix { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
}