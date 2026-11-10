using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;

namespace TP_pharmacy.Pages.Cart;

[Authorize(Roles = "Customer")]
public class CartIndexModel(PharmacyDbContext context) : PageModel
{
    public Models.Cart? Cart { get; set; }
    public decimal TotalCost => Cart?.CartItems.Sum(i => i.Product.Price * i.Quantity) ?? 0;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostRemoveAsync(int cartItemId)
    {
        var item = await context.CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cart.CustomerId == UserId);
        if (item == null) return NotFound();
        context.CartItems.Remove(item); await context.SaveChangesAsync(); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostUpdateQuantityAsync(int cartItemId, int quantity)
    {
        var item = await context.CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cart.CustomerId == UserId);
        if (item == null) return NotFound();
        var today = DateTime.UtcNow.Date;
        var available = await context.Stocks.Where(s => s.ProductId == item.ProductId && s.ExpiryDate >= today)
            .GroupBy(s => s.PharmacyId).Select(g => g.Sum(s => s.Quantity)).ToListAsync();
        if (quantity < 1 || quantity > 10000 || quantity > available.DefaultIfEmpty(0).Max())
        {
            ModelState.AddModelError("", "Количество должно быть положительным и не превышать наличие в одной аптеке.");
            await LoadAsync(); return Page();
        }
        item.Quantity = quantity; await context.SaveChangesAsync(); return RedirectToPage();
    }
    private async Task LoadAsync() => Cart = await context.Carts.AsNoTracking().Include(c => c.CartItems).ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(c => c.CustomerId == UserId);
}
