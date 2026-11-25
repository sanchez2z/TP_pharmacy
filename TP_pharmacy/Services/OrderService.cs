using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TP_pharmacy.Data;
using TP_pharmacy.Models;

namespace TP_pharmacy.Services;

public class BusinessException(string message) : Exception(message);

public class OrderService(PharmacyDbContext db, ILogger<OrderService>? logger = null)
{
    private ILogger<OrderService> Logger => logger ?? NullLogger<OrderService>.Instance;

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
        Logger.LogInformation("Начато создание заказа. CustomerId = {CustomerId}, PharmacyId = {PharmacyId}", customerId, pharmacyId);
        
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
        
        Logger.LogInformation(
            "Заказ успешно создан. OrderId={OrderId}, CustomerId={CustomerId}, TotalCost={TotalCost}",
            order.Id,
            customerId,
            order.TotalCost);
        
        return order;
    });

    public Task<bool> CancelAsync(string customerId, int orderId) => WriteAsync(async () =>
    {
        Logger.LogInformation(
            "Начата отмена заказа. CustomerId={CustomerId}, OrderId={OrderId}",
            customerId,
            orderId);
        
        var order = await db.Orders.Include(o => o.ReservedStocks).ThenInclude(s => s.Stock)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);
        if (order == null) return false;
        if (order.Status != OrderStatus.Pending) throw new BusinessException("Можно отменить только новый заказ.");
        if (order.ReservedStocks.Count == 0) throw new BusinessException("У заказа нет данных о резервировании партий. Требуется проверка менеджером.");
        foreach (var reservation in order.ReservedStocks) reservation.Stock.Quantity += reservation.Quantity;
        if (order.BonusSpent > 0)
        {
            var card = await db.DiscountCards.SingleAsync(c => c.CustomerId == customerId);
            card.BonusBalance += order.BonusSpent;
            db.BonusTransactions.Add(new BonusTransaction { DiscountCardId = card.Id, Amount = order.BonusSpent,
                Type = TransactionType.Earned, Description = $"Возврат бонусов за отменённый заказ №{order.Id}" });
        }
        order.Status = OrderStatus.Cancelled;
        
        Logger.LogInformation(
            "Заказ отменён. OrderId={OrderId}, Возвращено бонусов={BonusAmount}",
            order.Id,
            order.BonusSpent);
        
        return true;
    });

    public Task<bool> AdvanceAsync(string sellerId, int orderId, OrderStatus next) => WriteAsync(async () =>
    {
        Logger.LogInformation(
            "Изменение статуса заказа. SellerId={SellerId}, OrderId={OrderId}, NewStatus={NewStatus}",
            sellerId,
            orderId,
            next);
        
        var seller = await db.Sellers.FindAsync(sellerId);
        if (seller == null) return false;
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.PharmacyId == seller.PharmacyId);
        if (order == null) return false;
        var allowed = (order.Status, next) is (OrderStatus.Pending, OrderStatus.Processing) or
            (OrderStatus.Processing, OrderStatus.Ready) or (OrderStatus.Ready, OrderStatus.Completed);
        if (!allowed)
        {
            Logger.LogWarning(
                "Отклонён недопустимый переход статуса. OrderId={OrderId}, CurrentStatus={CurrentStatus}, RequestedStatus={RequestedStatus}",
                order.Id,
                order.Status,
                next);
            throw new BusinessException("Недопустимый переход статуса заказа.");
        }
        order.Status = next;
        if (next == OrderStatus.Completed) order.CompletedAt = DateTime.UtcNow;
        
        Logger.LogInformation(
            "Статус заказа изменён. OrderId={OrderId}, Status={Status}",
            order.Id,
            order.Status);
        
        return true;
    });

    public Task<Purchase> SellAsync(string sellerId, int productId, int quantity, string? phone, int? prescriptionId) => WriteAsync(async () =>
    {
        Logger.LogInformation(
            "Начата продажа товара. SellerId={SellerId}, ProductId={ProductId}, Quantity={Quantity}",
            sellerId,
            productId,
            quantity);
        
        var seller = await db.Sellers.FindAsync(sellerId) ?? throw new BusinessException("Продавец не найден.");
        var product = await db.Products.FindAsync(productId) ?? throw new BusinessException("Товар не найден.");
        if (quantity < 1 || quantity > 10000) throw new BusinessException("Количество должно быть от 1 до 10000.");
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
                CustomerPhoneNumber = phone, TotalCost = product.Price * quantity, Status = PrescriptionOrderStatus.Completed });
        }
        await TakeStockAsync(seller.PharmacyId, productId, quantity);
        var purchase = new Purchase { SellerId = sellerId, CustomerPhoneNumber = phone, TotalCost = product.Price * quantity };
        purchase.PurchaseItems.Add(new PurchaseItem { ProductId = productId, Quantity = quantity, Price = product.Price });
        db.Purchases.Add(purchase);
        
        Logger.LogInformation(
            "Продажа завершена. PurchaseId={PurchaseId}, ProductId={ProductId}, TotalCost={TotalCost}",
            purchase.Id,
            productId,
            purchase.TotalCost);
        
        return purchase;
    });

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
