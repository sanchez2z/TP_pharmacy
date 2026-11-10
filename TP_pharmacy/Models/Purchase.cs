using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public class Purchase
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SellerId { get; set; } = string.Empty;

    [ForeignKey("SellerId")]
    public Seller Seller { get; set; } = null!;

    [StringLength(20)]
    [Phone]
    public string? CustomerPhoneNumber { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
