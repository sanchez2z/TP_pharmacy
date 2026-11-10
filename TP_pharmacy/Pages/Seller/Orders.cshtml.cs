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
public class OrdersModel(PharmacyDbContext context, OrderService service) : PageModel
{
    public List<Order> Orders { get; set; } = new();
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> OnGetAsync()
    {
        var seller = await context.Sellers.FindAsync(UserId);
        if (seller == null) return Forbid();
        Orders = await context.Orders.AsNoTracking().Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Where(o => o.PharmacyId == seller.PharmacyId).OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Page();
    }
    public Task<IActionResult> OnPostProcessAsync(int orderId) => AdvanceAsync(orderId, OrderStatus.Processing);
    public Task<IActionResult> OnPostReadyAsync(int orderId) => AdvanceAsync(orderId, OrderStatus.Ready);
    public Task<IActionResult> OnPostCompleteAsync(int orderId) => AdvanceAsync(orderId, OrderStatus.Completed);
    private async Task<IActionResult> AdvanceAsync(int id, OrderStatus status)
    {
        try
        {
            if (!await service.AdvanceAsync(UserId, id, status)) return NotFound();
            return RedirectToPage();
        }
        catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); return await OnGetAsync(); }
    }
}
