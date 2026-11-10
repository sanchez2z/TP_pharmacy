using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TP_pharmacy.Models;

public class Prescription
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public int ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product Product { get; set; } = null!;

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    [StringLength(200)]
    public string? MedicalInstitution { get; set; }

    [StringLength(200)]
    public string? DoctorName { get; set; }

    [StringLength(500)]
    public string? Frequency { get; set; }

    public ICollection<PrescriptionOrder> PrescriptionOrders { get; set; } = new List<PrescriptionOrder>();
}
