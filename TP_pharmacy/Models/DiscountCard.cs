using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public class DiscountCard
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [ForeignKey("CustomerId")]
    public Customer Customer { get; set; } = null!;

    [Required]
    public int BonusBalance { get; set; } = 0;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BonusTransaction> BonusTransactions { get; set; } = new List<BonusTransaction>();
}
