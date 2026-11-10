using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Orders;

[Authorize(Roles = "Customer")]
public class OrdersIndexModel(PharmacyDbContext context) : PageModel
{
    public List<Order> Orders { get; set; } = new();
    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Orders = await context.Orders.AsNoTracking().Include(o => o.Pharmacy).Where(o => o.CustomerId == userId)
            .OrderByDescending(o => o.CreatedAt).ToListAsync();
    }
}
