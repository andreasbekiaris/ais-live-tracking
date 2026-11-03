using System.ComponentModel.DataAnnotations;

namespace InfraAis.Models.Auth;

public class CreateApiClientRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int PlanId { get; set; }
}