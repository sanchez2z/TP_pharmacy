using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Manager;

[Authorize(Roles = "Manager")]
public class StockModel(PharmacyDbContext context) : PageModel
{
    public List<Stock> Stocks { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<Pharmacy> Pharmacies { get; set; } = new();
    [BindProperty] public StockInput Input { get; set; } = new();
    public class StockInput
    {
        public int Id { get; set; }
        [Range(1, int.MaxValue)] public int PharmacyId { get; set; }
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Range(0, 1000000)] public int Quantity { get; set; }
        [Required, DataType(DataType.Date)] public DateTime? ExpiryDate { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var stock = await context.Stocks.FindAsync(id.Value);
            if (stock == null) return NotFound();
            Input = new StockInput { Id = stock.Id, PharmacyId = stock.PharmacyId, ProductId = stock.ProductId,
                Quantity = stock.Quantity, ExpiryDate = stock.ExpiryDate.Date };
        }
        await LoadAsync(); return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!await context.Products.AnyAsync(p => p.Id == Input.ProductId)) ModelState.AddModelError("Input.ProductId", "Товар не найден.");
        if (!await context.Pharmacies.AnyAsync(p => p.Id == Input.PharmacyId)) ModelState.AddModelError("Input.PharmacyId", "Аптека не найдена.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        if (Input.Id != 0 && await context.OrderStocks.AnyAsync(s => s.StockId == Input.Id))
        {
            ModelState.AddModelError("", "Партия связана с заказом: изменение запрещено для сохранности резерва. Создайте новую партию.");
            await LoadAsync(); return Page();
        }
        var stock = Input.Id == 0 ? new Stock() : await context.Stocks.FindAsync(Input.Id);
        if (stock == null) return NotFound();
        stock.PharmacyId = Input.PharmacyId; stock.ProductId = Input.ProductId; stock.Quantity = Input.Quantity;
        stock.ExpiryDate = DateTime.SpecifyKind(Input.ExpiryDate!.Value.Date, DateTimeKind.Utc);
        // Stock is a batch: multiple expiry dates for the same product/pharmacy are valid.
        if (Input.Id == 0) context.Stocks.Add(stock);
        await context.SaveChangesAsync(); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var stock = await context.Stocks.FindAsync(id);
        if (stock == null) return NotFound();
        if (await context.OrderStocks.AnyAsync(s => s.StockId == id))
        {
            ModelState.Clear(); ModelState.AddModelError("", "Партия связана с заказом и не может быть удалена.");
            await LoadAsync(); return Page();
        }
        context.Stocks.Remove(stock); await context.SaveChangesAsync(); return RedirectToPage();
    }
    private async Task LoadAsync()
    {
        Stocks = await context.Stocks.AsNoTracking().Include(s => s.Product).Include(s => s.Pharmacy)
            .OrderBy(s => s.Pharmacy.Address).ThenBy(s => s.Product.Name).ThenBy(s => s.ExpiryDate).ToListAsync();
        Products = await context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        Pharmacies = await context.Pharmacies.AsNoTracking().OrderBy(p => p.Address).ToListAsync();
    }
}
