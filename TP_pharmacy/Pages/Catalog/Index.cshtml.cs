using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;
using System.Security.Claims;
using TP_pharmacy.Services;

namespace TP_pharmacy.Pages.Catalog;

public class CatalogIndexModel(PharmacyDbContext context, CartService cartService) : PageModel
{
    public List<Product> Products { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? CurrentCategory { get; set; }
    public string? SearchTerm { get; set; }
    public bool PrescriptionOnly { get; set; }
    public Dictionary<int, int> AvailableQuantities { get; set; } = new();
    public HashSet<int> CartProductIds { get; set; } = new();
    [TempData] public string? CartMessage { get; set; }

    public async Task<IActionResult> OnPostAddToCartAsync(int productId, int quantity, string? searchTerm, string? category, bool prescriptionOnly = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (!User.IsInRole("Customer")) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await cartService.AddAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, productId, quantity);
                CartMessage = "Товар добавлен в корзину.";
                return RedirectToPage(new { searchTerm, category, prescriptionOnly });
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await OnGetAsync(searchTerm, category, prescriptionOnly);
        return Page();
    }
    public async Task OnGetAsync(string? searchTerm, string? category, bool prescriptionOnly = false)
    {
        SearchTerm = searchTerm?.Trim(); CurrentCategory = category; PrescriptionOnly = prescriptionOnly;
        var query = context.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(SearchTerm)) query = query.Where(p => p.Name.ToLower().Contains(SearchTerm.ToLower()));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);
        if (prescriptionOnly) query = query.Where(p => p.RequiresPrescription);
        Products = await query.OrderBy(p => p.Name).ToListAsync();
        Categories = await context.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var batches = await context.Stocks.AsNoTracking().Where(s => s.ExpiryDate >= today && s.Quantity > 0)
            .GroupBy(s => new { s.ProductId, s.PharmacyId }).Select(g => new { g.Key.ProductId, Quantity = g.Sum(s => s.Quantity) }).ToListAsync();
        AvailableQuantities = batches.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.Max(b => b.Quantity));
        if (User.IsInRole("Customer"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            CartProductIds = (await context.CartItems.AsNoTracking().Where(i => i.Cart.CustomerId == userId)
                .Select(i => i.ProductId).ToListAsync()).ToHashSet();
        }
    }
}
