using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Services;

public class CartService(PharmacyDbContext context)
{
    public async Task AddAsync(string userId, int productId, int quantity)
    {
        if (quantity < 1 || quantity > 10000) throw new BusinessException("Количество должно быть от 1 до 10000.");
        var product = await context.Products.FindAsync(productId);
        if (product == null) throw new BusinessException("Товар не найден.");
        if (product.RequiresPrescription) throw new BusinessException("Рецептурные препараты оформляет продавец в аптеке при предъявлении рецепта.");
        var cart = await context.Carts.Include(c => c.CartItems).FirstOrDefaultAsync(c => c.CustomerId == userId);
        if (cart == null) throw new BusinessException("Корзина покупателя не найдена.");
        var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
        var total = (long)(item?.Quantity ?? 0) + quantity;
        var today = DateTime.UtcNow.Date;
        var quantities = await context.Stocks.Where(s => s.ProductId == productId && s.ExpiryDate >= today && s.Quantity > 0)
            .GroupBy(s => s.PharmacyId).Select(g => g.Sum(s => s.Quantity)).ToListAsync();
        if (total > 10000 || total > quantities.DefaultIfEmpty(0).Max())
            throw new BusinessException("Недостаточно товара в одной аптеке для выбранного количества.");
        if (item == null) cart.CartItems.Add(new CartItem { ProductId = productId, Quantity = quantity });
        else item.Quantity = (int)total;
        await context.SaveChangesAsync();
    }
}
