using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Manager;

[Authorize(Roles = "Manager")]
public class ProductsModel(PharmacyDbContext context) : PageModel
{
    public List<Product> Products { get; set; } = new();
    [BindProperty] public ProductInput Input { get; set; } = new();
    public class ProductInput
    {
        public int Id { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = "";
        [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true)] public decimal Price { get; set; }
        [Required, StringLength(100)] public string Category { get; set; } = "";
        [StringLength(1000)] public string? Description { get; set; }
        public bool RequiresPrescription { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var product = await context.Products.FindAsync(id.Value);
            if (product == null) return NotFound();
            Input = new ProductInput { Id = product.Id, Name = product.Name, Price = product.Price,
                Category = product.Category, Description = product.Description, RequiresPrescription = product.RequiresPrescription };
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var product = Input.Id == 0 ? new Product() : await context.Products.FindAsync(Input.Id);
        if (product == null) return NotFound();
        product.Name = Input.Name.Trim(); product.Price = Input.Price;
        product.Category = Input.Category.Trim(); product.Description = Input.Description?.Trim();
        product.RequiresPrescription = Input.RequiresPrescription;
        if (Input.Id == 0) context.Products.Add(product);
        await context.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product == null) return NotFound();
        // Never cascade-delete sales history, prescriptions, carts or stock through a product.
        if (await context.Stocks.AnyAsync(s => s.ProductId == id) ||
            await context.CartItems.AnyAsync(i => i.ProductId == id) ||
            await context.OrderItems.AnyAsync(i => i.ProductId == id) ||
            await context.PurchaseItems.AnyAsync(i => i.ProductId == id) ||
            await context.Prescriptions.AnyAsync(p => p.ProductId == id))
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Нельзя удалить товар: существуют связанные остатки, корзины, рецепты или продажи.");
            await LoadAsync(); return Page();
        }
        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync() => Products = await context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
}
