using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Manager;

[Authorize(Roles = "Manager")]
public class PharmaciesModel(PharmacyDbContext context) : PageModel
{
    public List<Pharmacy> Pharmacies { get; set; } = new();
    [BindProperty] public PharmacyInput Input { get; set; } = new();
    public class PharmacyInput
    {
        public int Id { get; set; }
        [Required, StringLength(300)] public string Address { get; set; } = "";
        [Required, Phone, StringLength(20)] public string PhoneNumber { get; set; } = "";
    }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var pharmacy = await context.Pharmacies.FindAsync(id.Value);
            if (pharmacy == null) return NotFound();
            Input = new PharmacyInput { Id = pharmacy.Id, Address = pharmacy.Address, PhoneNumber = pharmacy.PhoneNumber };
        }
        await LoadAsync(); return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var pharmacy = Input.Id == 0 ? new Pharmacy() : await context.Pharmacies.FindAsync(Input.Id);
        if (pharmacy == null) return NotFound();
        pharmacy.Address = Input.Address.Trim(); pharmacy.PhoneNumber = Input.PhoneNumber.Trim();
        if (Input.Id == 0) context.Pharmacies.Add(pharmacy);
        await context.SaveChangesAsync(); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var pharmacy = await context.Pharmacies.FindAsync(id);
        if (pharmacy == null) return NotFound();
        if (await context.Stocks.AnyAsync(s => s.PharmacyId == id) || await context.Sellers.AnyAsync(s => s.PharmacyId == id) ||
            await context.Orders.AnyAsync(o => o.PharmacyId == id))
        {
            ModelState.Clear(); ModelState.AddModelError("", "Аптека используется в остатках, сотрудниках или заказах.");
            await LoadAsync(); return Page();
        }
        context.Pharmacies.Remove(pharmacy); await context.SaveChangesAsync(); return RedirectToPage();
    }
    private async Task LoadAsync() => Pharmacies = await context.Pharmacies.AsNoTracking().OrderBy(p => p.Address).ToListAsync();
}
