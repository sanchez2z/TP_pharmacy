using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Seller;

[Authorize(Roles = "Seller")]
public class DashboardModel(PharmacyDbContext context) : PageModel
{
    public int NewOrdersCount { get; set; }
    public int TodaySalesCount { get; set; }
    public int PendingPrescriptionsCount { get; set; }
    public string PharmacyAddress { get; set; } = "";
    public async Task<IActionResult> OnGetAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var seller = await context.Sellers.AsNoTracking().Include(s => s.Pharmacy).FirstOrDefaultAsync(s => s.UserId == id);
        if (seller == null) return Forbid();
        PharmacyAddress = seller.Pharmacy.Address;
        NewOrdersCount = await context.Orders.CountAsync(o => o.PharmacyId == seller.PharmacyId && o.Status == OrderStatus.Pending);
        var today = DateTime.UtcNow.Date;
        TodaySalesCount = await context.Purchases.CountAsync(p => p.SellerId == id && p.PurchaseDate >= today);
        PendingPrescriptionsCount = await context.PrescriptionOrders.CountAsync(p => p.SellerId == id && p.Status == PrescriptionOrderStatus.Pending);
        return Page();
    }
}
