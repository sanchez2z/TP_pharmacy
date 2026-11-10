using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Seller;

[Authorize(Roles = "Seller")]
public class PrescriptionsModel(PharmacyDbContext context) : PageModel
{
    public List<Prescription> Prescriptions { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    [BindProperty] public PrescriptionInput Input { get; set; } = new();
    public class PrescriptionInput
    {
        public int Id { get; set; }
        [Required, StringLength(50)] public string SerialNumber { get; set; } = "";
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Required, DataType(DataType.Date)] public DateTime? IssueDate { get; set; }
        [Required, DataType(DataType.Date)] public DateTime? ExpiryDate { get; set; }
        [Required, StringLength(200)] public string MedicalInstitution { get; set; } = "";
        [Required, StringLength(200)] public string DoctorName { get; set; } = "";
        [StringLength(500)] public string? Frequency { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var p = await context.Prescriptions.FindAsync(id.Value);
            if (p == null) return NotFound();
            Input = new PrescriptionInput { Id = p.Id, SerialNumber = p.SerialNumber, ProductId = p.ProductId, IssueDate = p.IssueDate,
                ExpiryDate = p.ExpiryDate, MedicalInstitution = p.MedicalInstitution ?? "", DoctorName = p.DoctorName ?? "", Frequency = p.Frequency };
        }
        await LoadAsync(); return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!await context.Products.AnyAsync(p => p.Id == Input.ProductId && p.RequiresPrescription))
            ModelState.AddModelError("Input.ProductId", "Выберите рецептурный препарат.");
        if (Input.IssueDate > DateTime.UtcNow.Date || Input.ExpiryDate < Input.IssueDate)
            ModelState.AddModelError("Input.ExpiryDate", "Проверьте дату выдачи и срок действия рецепта.");
        var serialNumber = Input.SerialNumber?.Trim();
        if (await context.Prescriptions.AnyAsync(p => p.SerialNumber == serialNumber && p.Id != Input.Id))
            ModelState.AddModelError("Input.SerialNumber", "Рецепт с таким номером уже существует.");
        if (Input.Id != 0 && await context.PrescriptionOrders.AnyAsync(p => p.PrescriptionId == Input.Id))
            ModelState.AddModelError("", "Использованный рецепт нельзя изменять.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var prescription = Input.Id == 0 ? new Prescription() : await context.Prescriptions.FindAsync(Input.Id);
        if (prescription == null) return NotFound();
        prescription.SerialNumber = serialNumber!; prescription.ProductId = Input.ProductId;
        prescription.IssueDate = DateTime.SpecifyKind(Input.IssueDate!.Value.Date, DateTimeKind.Utc);
        prescription.ExpiryDate = DateTime.SpecifyKind(Input.ExpiryDate!.Value.Date, DateTimeKind.Utc);
        prescription.MedicalInstitution = Input.MedicalInstitution.Trim(); prescription.DoctorName = Input.DoctorName.Trim(); prescription.Frequency = Input.Frequency?.Trim();
        if (Input.Id == 0) context.Prescriptions.Add(prescription);
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            ModelState.AddModelError("", "Рецепт с таким номером уже существует."); await LoadAsync(); return Page();
        }
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var prescription = await context.Prescriptions.FindAsync(id);
        if (prescription == null) return NotFound();
        if (await context.PrescriptionOrders.AnyAsync(p => p.PrescriptionId == id))
        {
            ModelState.Clear(); ModelState.AddModelError("", "Рецепт связан с продажей и не может быть удалён."); await LoadAsync(); return Page();
        }
        context.Prescriptions.Remove(prescription); await context.SaveChangesAsync(); return RedirectToPage();
    }
    private async Task LoadAsync()
    {
        Prescriptions = await context.Prescriptions.AsNoTracking().Include(p => p.Product).OrderByDescending(p => p.IssueDate).ToListAsync();
        Products = await context.Products.AsNoTracking().Where(p => p.RequiresPrescription).OrderBy(p => p.Name).ToListAsync();
    }
}
