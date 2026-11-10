using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Catalog;

public class DetailsModel(PharmacyDbContext context) : PageModel
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await context.Carts.Include(c => c.CartItems).FirstOrDefaultAsync(c => c.CustomerId == userId);
        if (cart == null) return NotFound();
        var item = cart.CartItems.FirstOrDefault(i => i.ProductId == id);
        var totalQuantity = (item?.Quantity ?? 0) + Quantity;
        var available = Stocks.GroupBy(s => s.PharmacyId).Select(g => g.Sum(s => s.Quantity)).DefaultIfEmpty(0).Max();
        if (totalQuantity > available)
        {
            ModelState.AddModelError("", "Недостаточно товара в одной аптеке для выбранного количества."); return Page();
        }
        if (item == null) cart.CartItems.Add(new CartItem { ProductId = id, Quantity = Quantity });
        else item.Quantity = totalQuantity;
        await context.SaveChangesAsync(); return RedirectToPage("/Cart/Index");
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
