using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Services;

public class BusinessException(string message) : Exception(message);

public class OrderService(PharmacyDbContext db)
{
    private async Task<T> WriteAsync<T>(Func<Task<T>> action)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await action();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex) when (ex is PostgresException { SqlState: "40001" or "40P01" } ||
            ex is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } })
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            throw new BusinessException("Данные изменились другим пользователем. Обновите страницу и повторите операцию.");
        }
    }

    public Task<Order> CreateAsync(string customerId, int pharmacyId, int bonusAmount) => WriteAsync(async () =>
    {
        var cart = await db.Carts.Include(c => c.CartItems).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (cart == null || cart.CartItems.Count == 0) throw new BusinessException("Корзина пуста.");
        if (!await db.Pharmacies.AnyAsync(p => p.Id == pharmacyId)) throw new BusinessException("Аптека не найдена.");
        if (cart.CartItems.Any(i => i.Product.RequiresPrescription)) throw new BusinessException("Рецептурные препараты оформляются продавцом при личном обращении.");
        if (cart.CartItems.Any(i => i.Quantity < 1 || i.Quantity > 10000)) throw new BusinessException("Некорректное количество товара.");
        var total = cart.CartItems.Sum(i => i.Quantity * i.Product.Price);
        var card = await db.DiscountCards.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (bonusAmount < 0 || bonusAmount > Math.Floor(total / 2) || bonusAmount > (card?.BonusBalance ?? 0))
            throw new BusinessException("Списание превышает баланс карты или 50% стоимости заказа.");
        var order = new Order { CustomerId = customerId, PharmacyId = pharmacyId, TotalCost = total - bonusAmount, BonusSpent = bonusAmount };
        foreach (var item in cart.CartItems)
        {
            var allocations = await TakeStockAsync(pharmacyId, item.ProductId, item.Quantity);
            foreach (var (stock, quantity) in allocations)
                order.ReservedStocks.Add(new OrderStock { StockId = stock.Id, Quantity = quantity });
            order.OrderItems.Add(new OrderItem { ProductId = item.ProductId, Quantity = item.Quantity, Price = item.Product.Price });
        }
        if (bonusAmount > 0)
        {
            card!.BonusBalance -= bonusAmount;
            db.BonusTransactions.Add(new BonusTransaction { DiscountCardId = card.Id, Amount = bonusAmount,
                Type = TransactionType.Spent, Description = "Оплата интернет-заказа" });
        }
        db.Orders.Add(order); db.CartItems.RemoveRange(cart.CartItems);
        return order;
    });

    public Task<bool> CancelAsync(string customerId, int orderId) => WriteAsync(async () =>
    {
        var order = await db.Orders.Include(o => o.ReservedStocks).ThenInclude(s => s.Stock)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);
        if (order == null) return false;
        await CancelOrderAsync(order);
        return true;
    });

    private async Task CancelOrderAsync(Order order)
    {
        if (!order.Status.CanCancel()) throw new BusinessException("Завершённый или уже отменённый заказ нельзя отменить.");
        if (order.ReservedStocks.Count == 0) throw new BusinessException("У заказа нет данных о резервировании партий. Требуется проверка менеджером.");
        foreach (var reservation in order.ReservedStocks) reservation.Stock.Quantity += reservation.Quantity;
        if (order.BonusSpent > 0)
        {
            var card = await db.DiscountCards.SingleAsync(c => c.CustomerId == order.CustomerId);
            card.BonusBalance += order.BonusSpent;
            db.BonusTransactions.Add(new BonusTransaction { DiscountCardId = card.Id, Amount = order.BonusSpent,
                Type = TransactionType.Earned, Description = $"Возврат бонусов за отменённый заказ №{order.Id}" });
        }
        order.Status = OrderStatus.Cancelled;
    }

    public Task<bool> CancelBySellerAsync(string sellerId, int orderId) => WriteAsync(async () =>
    {
        var seller = await db.Sellers.FindAsync(sellerId);
        if (seller == null) return false;
        var order = await db.Orders.Include(o => o.ReservedStocks).ThenInclude(s => s.Stock)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.PharmacyId == seller.PharmacyId);
        if (order == null) return false;
        await CancelOrderAsync(order);
        return true;
    });

    public Task<bool> AdvanceAsync(string sellerId, int orderId, OrderStatus next) => WriteAsync(async () =>
    {
        var seller = await db.Sellers.FindAsync(sellerId);
        if (seller == null) return false;
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.PharmacyId == seller.PharmacyId);
        if (order == null) return false;
        var allowed = (order.Status, next) is (OrderStatus.Pending, OrderStatus.Processing) or
            (OrderStatus.Processing, OrderStatus.Ready) or (OrderStatus.Ready, OrderStatus.Completed);
        if (!allowed) throw new BusinessException("Недопустимый переход статуса заказа.");
        order.Status = next;
        if (next == OrderStatus.Completed)
        {
            order.CompletedAt = DateTime.UtcNow;
            var card = await db.DiscountCards.SingleOrDefaultAsync(c => c.CustomerId == order.CustomerId);
            if (card != null) EarnBonuses(card, order.TotalCost, $"Начисление за завершённый заказ №{order.Id}");
        }
        return true;
    });

    public Task<Purchase> SellAsync(string sellerId, int productId, int quantity, string? phone, int? prescriptionId, int bonusAmount = 0) => WriteAsync(async () =>
    {
        var seller = await db.Sellers.FindAsync(sellerId) ?? throw new BusinessException("Продавец не найден.");
        var product = await db.Products.FindAsync(productId) ?? throw new BusinessException("Товар не найден.");
        if (quantity < 1 || quantity > 10000) throw new BusinessException("Количество должно быть от 1 до 10000.");
        var total = product.Price * quantity;
        DiscountCard? card = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var normalized = NormalizePhone(phone);
            var cards = await db.DiscountCards.Include(c => c.Customer).ThenInclude(c => c.User).ToListAsync();
            var matches = cards.Where(c => NormalizePhone(c.Customer.User.PhoneNumber) == normalized).ToList();
            if (matches.Count > 1) throw new BusinessException("Телефон привязан к нескольким картам. Уточните данные покупателя.");
            card = matches.SingleOrDefault();
        }
        if (bonusAmount < 0 || bonusAmount > Math.Floor(total / 2) || bonusAmount > (card?.BonusBalance ?? 0))
            throw new BusinessException("Списание превышает баланс карты или 50% стоимости продажи. Проверьте телефон покупателя.");
        if (product.RequiresPrescription)
        {
            var prescription = await db.Prescriptions.FindAsync(prescriptionId ?? 0);
            var today = DateTime.UtcNow.Date;
            if (prescription == null || prescription.ProductId != productId || prescription.IssueDate > today || prescription.ExpiryDate < today)
                throw new BusinessException("Предъявите действующий рецепт на выбранный препарат.");
            if (string.IsNullOrWhiteSpace(phone)) throw new BusinessException("Для рецептурной продажи нужен телефон покупателя.");
            if (await db.PrescriptionOrders.AnyAsync(p => p.PrescriptionId == prescription.Id && p.Status == PrescriptionOrderStatus.Completed))
                throw new BusinessException("Рецепт уже использован. Повторный отпуск требует отдельной проверки.");
            db.PrescriptionOrders.Add(new PrescriptionOrder { PrescriptionId = prescription.Id, SellerId = sellerId,
                CustomerPhoneNumber = phone, TotalCost = total - bonusAmount, Status = PrescriptionOrderStatus.Completed });
        }
        await TakeStockAsync(seller.PharmacyId, productId, quantity);
        var purchase = new Purchase { SellerId = sellerId, CustomerPhoneNumber = phone, TotalCost = total - bonusAmount };
        purchase.PurchaseItems.Add(new PurchaseItem { ProductId = productId, Quantity = quantity, Price = product.Price });
        db.Purchases.Add(purchase);
        if (card != null)
        {
            // Obtain the sale ID inside the same transaction for an auditable description.
            await db.SaveChangesAsync();
            if (bonusAmount > 0)
            {
                card.BonusBalance -= bonusAmount;
                db.BonusTransactions.Add(new BonusTransaction { DiscountCardId = card.Id, Amount = bonusAmount,
                    Type = TransactionType.Spent, Description = $"Списание за продажу №{purchase.Id}" });
            }
            EarnBonuses(card, purchase.TotalCost, $"Начисление за завершённую продажу №{purchase.Id}");
        }
        return purchase;
    });

    private void EarnBonuses(DiscountCard card, decimal paid, string description)
    {
        var amount = (int)Math.Floor(paid * 0.05m);
        if (amount == 0) return;
        card.BonusBalance += amount;
        db.BonusTransactions.Add(new BonusTransaction { DiscountCardId = card.Id, Amount = amount,
            Type = TransactionType.Earned, Description = description });
    }

    private static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 11 && digits[0] == '8' ? "7" + digits[1..] : digits;
    }

    private async Task<List<(Stock Stock, int Quantity)>> TakeStockAsync(int pharmacyId, int productId, int quantity)
    {
        var today = DateTime.UtcNow.Date;
        var stocks = await db.Stocks.Where(s => s.PharmacyId == pharmacyId && s.ProductId == productId && s.ExpiryDate >= today && s.Quantity > 0)
            .OrderBy(s => s.ExpiryDate).ThenBy(s => s.Id).ToListAsync();
        if (stocks.Sum(s => s.Quantity) < quantity) throw new BusinessException("В выбранной аптеке недостаточно годного товара.");
        var allocations = new List<(Stock, int)>();
        foreach (var stock in stocks)
        {
            var taken = Math.Min(quantity, stock.Quantity);
            stock.Quantity -= taken; quantity -= taken; allocations.Add((stock, taken));
            if (quantity == 0) break;
        }
        return allocations;
    }
}
