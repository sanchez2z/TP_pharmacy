using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public class Cart
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [ForeignKey("CustomerId")]
    public Customer Customer { get; set; } = null!;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
