using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public enum OrderStatus
{
    Pending,
    Processing,
    Ready,
    Completed,
    Cancelled
}

public class Order
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [ForeignKey("CustomerId")]
    public Customer Customer { get; set; } = null!;

    [Required]
    public int PharmacyId { get; set; }

    [ForeignKey("PharmacyId")]
    public Pharmacy Pharmacy { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalCost { get; set; }

    public int BonusSpent { get; set; }

    public ICollection<OrderStock> ReservedStocks { get; set; } = new List<OrderStock>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
