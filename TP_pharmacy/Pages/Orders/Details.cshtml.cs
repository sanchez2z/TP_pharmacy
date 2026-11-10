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
public class DetailsModel(PharmacyDbContext context, OrderService orders) : PageModel
{
    public Order Order { get; set; } = null!;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await context.Orders.AsNoTracking().Include(o => o.Pharmacy).Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == UserId);
        if (order == null) return NotFound();
        Order = order; return Page();
    }
    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        try
        {
            if (!await orders.CancelAsync(UserId, id)) return NotFound();
            return RedirectToPage(new { id });
        }
        catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); return await OnGetAsync(id); }
    }
}
