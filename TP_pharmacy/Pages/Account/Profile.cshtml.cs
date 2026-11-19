using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Account;

[Authorize]
public class ProfileModel(PharmacyDbContext context) : PageModel
{
    public Models.User Account { get; private set; } = null!;
    public List<Order> Orders { get; private set; } = new();
    public DiscountCard? Card { get; private set; }
    public List<BonusTransaction> Transactions { get; private set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (account == null) return NotFound();
        Account = account;
        // Never accept a user ID from query/form data: only the authenticated user's profile.
        Orders = await context.Orders.AsNoTracking().Include(o => o.Pharmacy).Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Where(o => o.CustomerId == userId).OrderByDescending(o => o.CreatedAt).ToListAsync();
        Card = await context.DiscountCards.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == userId);
        if (Card != null)
            Transactions = await context.BonusTransactions.AsNoTracking().Where(t => t.DiscountCardId == Card.Id)
                .OrderByDescending(t => t.TransactionDate).Take(50).ToListAsync();
        return Page();
    }
}
