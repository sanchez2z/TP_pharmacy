using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TP_pharmacy.Data;
using Microsoft.EntityFrameworkCore;

namespace TP_pharmacy.Pages.Manager;

[Authorize(Roles = "Manager")]
public class ManagerDashboardModel : PageModel
{
    private readonly PharmacyDbContext _context;

    public ManagerDashboardModel(PharmacyDbContext context)
    {
        _context = context;
    }

    public int ProductsCount { get; set; }
    public int StocksCount { get; set; }
    public int EmployeesCount { get; set; }

    public async Task OnGetAsync()
    {
        ProductsCount = await _context.Products.CountAsync();
        StocksCount = await _context.Stocks.CountAsync();
        EmployeesCount = await _context.Sellers.CountAsync();
    }
}
