using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;
using TP_pharmacy.Services;

namespace TP_pharmacy.Pages.Seller;

[Authorize(Roles = "Seller")]
public class SalesModel(PharmacyDbContext context, OrderService orders) : PageModel
{
    [BindProperty, Phone, StringLength(20)] public string? CustomerPhone { get; set; }
    [BindProperty, Range(1, int.MaxValue)] public int ProductId { get; set; }
    [BindProperty, Range(1, 10000)] public int Quantity { get; set; } = 1;
    [BindProperty] public int? PrescriptionId { get; set; }
    [BindProperty, Range(0, int.MaxValue)] public int BonusAmount { get; set; }
    public List<Product> Products { get; set; } = new();
    public List<Purchase> Purchases { get; set; } = new();
    [TempData] public string? Message { get; set; }
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task OnGetAsync(int? productId)
    {
        if (productId.HasValue) ProductId = productId.Value;
        await LoadAsync();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                var purchase = await orders.SellAsync(UserId, ProductId, Quantity, CustomerPhone, PrescriptionId, BonusAmount);
                Message = $"Продажа №{purchase.Id} сохранена."; return RedirectToPage();
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await LoadAsync(); return Page();
    }
    private async Task LoadAsync()
    {
        var pharmacyId = await context.Sellers.Where(s => s.UserId == UserId).Select(s => s.PharmacyId).FirstOrDefaultAsync();
        Products = await context.Products.AsNoTracking().Where(p => p.Stocks.Any(s => s.PharmacyId == pharmacyId)).OrderBy(p => p.Name).ToListAsync();
        Purchases = await context.Purchases.AsNoTracking().Include(p => p.PurchaseItems).ThenInclude(i => i.Product)
            .Where(p => p.SellerId == UserId).OrderByDescending(p => p.PurchaseDate).Take(100).ToListAsync();
    }
}
