using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Pages.Catalog;

public class CatalogIndexModel(PharmacyDbContext context) : PageModel
{
    public List<Product> Products { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? CurrentCategory { get; set; }
    public string? SearchTerm { get; set; }
    public bool PrescriptionOnly { get; set; }
    public async Task OnGetAsync(string? searchTerm, string? category, bool prescriptionOnly = false)
    {
        SearchTerm = searchTerm?.Trim(); CurrentCategory = category; PrescriptionOnly = prescriptionOnly;
        var query = context.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(SearchTerm)) query = query.Where(p => p.Name.ToLower().Contains(SearchTerm.ToLower()));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);
        if (prescriptionOnly) query = query.Where(p => p.RequiresPrescription);
        Products = await query.OrderBy(p => p.Name).ToListAsync();
        Categories = await context.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
    }
}
