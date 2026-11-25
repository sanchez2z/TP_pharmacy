using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PharmacyReports;
using TP_pharmacy.Data;

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
    public decimal Revenue { get; set; }
    public int SoldItems { get; set; }
    public decimal AverageCheck { get; set; }
    public SaleRecord? BestSellingProduct { get; set; }
    public int LowStockProductsCount { get; set; }

    public async Task OnGetAsync()
    {
        ProductsCount = await _context.Products.CountAsync();
        StocksCount = await _context.Stocks.CountAsync();
        EmployeesCount = await _context.Sellers.CountAsync();

        var sales = await _context.Purchases
            .AsNoTracking()
            .Include(p => p.PurchaseItems)
            .ThenInclude(item => item.Product)
            .SelectMany(p => p.PurchaseItems.Select(item => new SaleRecord(
                item.Product.Name,
                item.Product.Category,
                item.Quantity,
                item.Price * item.Quantity)))
            .ToListAsync();

        Revenue = SalesReportCalculator.CalculateRevenue(sales);
        SoldItems = SalesReportCalculator.CalculateSoldItems(sales);
        AverageCheck = SalesReportCalculator.CalculateAverageCheck(sales);
        BestSellingProduct = SalesReportCalculator.FindBestSellingProduct(sales);

        LowStockProductsCount = await _context.Stocks
            .AsNoTracking()
            .GroupBy(stock => stock.ProductId)
            .CountAsync(group => group.Sum(stock => stock.Quantity) < 10);
    }
}
