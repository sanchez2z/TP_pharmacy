using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public class Seller
{
    [Key]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Required]
    public int PharmacyId { get; set; }

    [ForeignKey("PharmacyId")]
    public Pharmacy Pharmacy { get; set; } = null!;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<PrescriptionOrder> PrescriptionOrders { get; set; } = new List<PrescriptionOrder>();
}
