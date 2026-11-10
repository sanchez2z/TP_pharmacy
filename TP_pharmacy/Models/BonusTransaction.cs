using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public enum TransactionType
{
    Earned,
    Spent
}

public class BonusTransaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DiscountCardId { get; set; }

    [ForeignKey("DiscountCardId")]
    public DiscountCard DiscountCard { get; set; } = null!;

    [Required]
    public int Amount { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? Description { get; set; }
}
