using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;

namespace TP_pharmacy.Pages.Seller;

[Authorize(Roles = "Seller")]
public class StockModel(PharmacyDbContext context) : PageModel
{
    public string PharmacyAddress { get; private set; } = "";
    public string? SearchTerm { get; private set; }
    public List<StockRow> Rows { get; private set; } = new();
    public record StockRow(int ProductId, string Name, string Category, decimal Price, bool RequiresPrescription,
        int Available, int Expired);

    public async Task<IActionResult> OnGetAsync(string? searchTerm)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var seller = await context.Sellers.AsNoTracking().Include(s => s.Pharmacy).FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return Forbid();
        PharmacyAddress = seller.Pharmacy.Address;
        SearchTerm = searchTerm?.Trim();
        // Pharmacy ID comes only from the seller's profile, never from request data.
        var products = context.Products.AsNoTracking().Where(p => p.Stocks.Any(s => s.PharmacyId == seller.PharmacyId));
        if (!string.IsNullOrWhiteSpace(SearchTerm)) products = products.Where(p => p.Name.ToLower().Contains(SearchTerm.ToLower()));
        var today = DateTime.UtcNow.Date;
        Rows = await products.OrderBy(p => p.Name).Select(p => new StockRow(p.Id, p.Name, p.Category, p.Price, p.RequiresPrescription,
            p.Stocks.Where(s => s.PharmacyId == seller.PharmacyId && s.ExpiryDate >= today).Sum(s => s.Quantity),
            p.Stocks.Where(s => s.PharmacyId == seller.PharmacyId && s.ExpiryDate < today).Sum(s => s.Quantity))).ToListAsync();
        return Page();
    }
}
