using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public enum PrescriptionOrderStatus
{
    Pending,
    Approved,
    Rejected,
    Completed
}

public class PrescriptionOrder
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PrescriptionId { get; set; }

    [ForeignKey("PrescriptionId")]
    public Prescription Prescription { get; set; } = null!;

    [Required]
    public string SellerId { get; set; } = string.Empty;

    [ForeignKey("SellerId")]
    public Seller Seller { get; set; } = null!;

    [Required]
    [StringLength(20)]
    [Phone]
    public string CustomerPhoneNumber { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Required]
    public PrescriptionOrderStatus Status { get; set; } = PrescriptionOrderStatus.Pending;
}
