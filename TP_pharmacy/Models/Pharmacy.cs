using System.ComponentModel.DataAnnotations;

namespace TP_pharmacy.Models;

public class Pharmacy
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<Seller> Sellers { get; set; } = new List<Seller>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
