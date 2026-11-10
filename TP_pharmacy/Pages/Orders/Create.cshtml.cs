using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;
using TP_pharmacy.Services;

namespace TP_pharmacy.Pages.Orders;

[Authorize(Roles = "Customer")]
public class CreateOrderModel(PharmacyDbContext context, OrderService orders) : PageModel
{
    [BindProperty, Range(1, int.MaxValue)] public int PharmacyId { get; set; }
    [BindProperty, Range(0, int.MaxValue)] public int BonusAmount { get; set; }
    public List<Pharmacy> Pharmacies { get; set; } = new();
    public Models.Cart? Cart { get; set; }
    public int BonusBalance { get; set; }
    public decimal TotalCost => Cart?.CartItems.Sum(i => i.Quantity * i.Product.Price) ?? 0;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                var order = await orders.CreateAsync(UserId, PharmacyId, BonusAmount);
                return RedirectToPage("./Details", new { id = order.Id });
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await LoadAsync(); return Page();
    }
    private async Task LoadAsync()
    {
        Pharmacies = await context.Pharmacies.AsNoTracking().OrderBy(p => p.Address).ToListAsync();
        Cart = await context.Carts.AsNoTracking().Include(c => c.CartItems).ThenInclude(i => i.Product).FirstOrDefaultAsync(c => c.CustomerId == UserId);
        BonusBalance = await context.DiscountCards.Where(c => c.CustomerId == UserId).Select(c => c.BonusBalance).FirstOrDefaultAsync();
    }
}
