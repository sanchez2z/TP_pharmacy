using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;
using TP_pharmacy.Services;

namespace TP_pharmacy.Pages.Catalog;

public class DetailsModel(PharmacyDbContext context, CartService cartService) : PageModel
{
    public Product Product { get; set; } = null!;
    public List<Stock> Stocks { get; set; } = new();
    [BindProperty, Range(1, 10000)] public int Quantity { get; set; } = 1;
    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!User.IsInRole("Customer")) return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        if (!await LoadAsync(id)) return NotFound();
        if (Product.RequiresPrescription) ModelState.AddModelError("", "Рецептурные препараты оформляет продавец в аптеке при предъявлении рецепта.");
        if (!ModelState.IsValid) return Page();
        try
        {
            await cartService.AddAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, Quantity);
            return RedirectToPage("/Cart/Index");
        }
        catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); return Page(); }
    }
    private async Task<bool> LoadAsync(int id)
    {
        var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return false;
        Product = product;
        var today = DateTime.UtcNow.Date;
        Stocks = await context.Stocks.AsNoTracking().Include(s => s.Pharmacy)
            .Where(s => s.ProductId == id && s.Quantity > 0 && s.ExpiryDate >= today).OrderBy(s => s.ExpiryDate).ToListAsync();
        return true;
    }
}
