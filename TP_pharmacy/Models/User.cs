using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TP_pharmacy.Models;

public class User : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

    public string? UserType { get; set; }
}
