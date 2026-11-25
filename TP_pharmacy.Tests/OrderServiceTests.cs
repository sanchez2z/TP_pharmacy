using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TP_pharmacy.Data;
using TP_pharmacy.Models;
using TP_pharmacy.Services;

namespace TP_pharmacy.Tests;

//TODO: починить юнит-тесты

[TestFixture]
[NonParallelizable]
public class OrderServiceTests
{
    private readonly string databaseName = "tp_pharmacy_tests_" + Guid.NewGuid().ToString("N");
    private string adminConnectionString = "Host=localhost;Database=tp_5sem;Username=postgres;Password=postgres";
    private DbContextOptions<PharmacyDbContext> options = null!;
    private PharmacyDbContext db = null!;
    private OrderService service = null!;
    private bool databaseCreated;
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [OneTimeSetUp]
    public async Task CreateTestDatabase()
    {
        var connectionString = adminConnectionString;

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres", Pooling = false, IncludeErrorDetail = false
        };
        adminConnectionString = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        databaseCreated = true;
        builder.Database = databaseName;
        options = new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(builder.ConnectionString).Options;
        await using var context = new PharmacyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        TestContext.Progress.WriteLine($"Test database: {databaseName}");
    }

    [SetUp]
    public async Task SetUp()
    {
        db = new PharmacyDbContext(options);
        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Distinct()
            .Select(name => "\"" + name!.Replace("\"", "\"\"") + "\"");
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE " + string.Join(", ", tables) + " RESTART IDENTITY CASCADE");
        db.Customers.Add(new Customer { UserId = "customer", User = new User { Id = "customer", FullName = "Test customer" } });
        db.Pharmacies.AddRange(new Pharmacy { Id = 1, Address = "Test pharmacy" }, new Pharmacy { Id = 2, Address = "Other pharmacy" });
        db.Sellers.Add(new Seller { UserId = "seller", User = new User { Id = "seller", FullName = "Test seller" }, PharmacyId = 1 });
        db.Products.Add(new Product { Id = 1, Name = "Test product", Price = 100m, Category = "Test" });
        db.Carts.Add(new Cart { CustomerId = "customer", CartItems = [new CartItem { ProductId = 1, Quantity = 2 }] });
        db.Stocks.Add(new Stock { Id = 1, PharmacyId = 1, ProductId = 1, Quantity = 10, ExpiryDate = Today.AddDays(30) });
        db.DiscountCards.Add(new DiscountCard { CustomerId = "customer", BonusBalance = 100 });
        await db.SaveChangesAsync();
        // Explicit seed IDs do not advance PostgreSQL's identity sequence.
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"Products\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Products\"))");
        db.ChangeTracker.Clear();
        service = new OrderService(db);
    }

    [TearDown]
    public async Task TearDown()
    {
        await db.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task DropTestDatabase()
    {
        if (!databaseCreated) return;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        TestContext.Progress.WriteLine($"Removed temporary database: {databaseName}");
    }

    [Test, Category("01_Checkout")]
    public async Task Create_ValidCart_PersistsOrderReservesStockAndClearsCart()
    {
        var result = await service.CreateAsync("customer", 1, 0);
        db.ChangeTracker.Clear();
        var order = await db.Orders.Include(o => o.OrderItems).Include(o => o.ReservedStocks).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(order.Id, Is.EqualTo(result.Id));
            Assert.That(order.CustomerId, Is.EqualTo("customer"));
            Assert.That(order.PharmacyId, Is.EqualTo(1));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(order.TotalCost, Is.EqualTo(200m));
            Assert.That(order.OrderItems.Single().Quantity, Is.EqualTo(2));
            Assert.That(order.OrderItems.Single().Price, Is.EqualTo(100m));
            Assert.That(order.ReservedStocks.Single().StockId, Is.EqualTo(1));
            Assert.That(order.ReservedStocks.Single().Quantity, Is.EqualTo(2));
        });
        Assert.That(await db.CartItems.CountAsync(), Is.Zero);
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(8));
    }

    [Test, Category("02_Validation")]
    public async Task Create_EmptyCart_IsRejected()
    {
        db.CartItems.RemoveRange(db.CartItems);
        await db.SaveChangesAsync();
        Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync("customer", 1, 0));
        Assert.That(await db.Orders.CountAsync(), Is.Zero);
    }

    [Test, Category("02_Validation")]
    public async Task Create_LaterItemUnavailable_RollsBackEarlierStockDeduction()
    {
        db.Products.Add(new Product { Id = 2, Name = "Unavailable", Price = 50m, Category = "Test" });
        db.CartItems.Add(new CartItem { CartId = (await db.Carts.SingleAsync()).Id, ProductId = 2, Quantity = 1 });
        await db.SaveChangesAsync();
        Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync("customer", 1, 0));
        db.ChangeTracker.Clear();
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(10));
        Assert.That(await db.CartItems.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.Orders.CountAsync(), Is.Zero);
    }

    [Test, Category("03_Bonuses")]
    [TestCase(50)]
    public async Task Create_ValidBonuses_UpdatesTotalBalanceAndLedger(int bonuses)
    {
        await service.CreateAsync("customer", 1, bonuses);
        db.ChangeTracker.Clear();
        var order = await db.Orders.SingleAsync();
        Assert.That(order.TotalCost, Is.EqualTo(200m - bonuses));
        Assert.That(order.BonusSpent, Is.EqualTo(bonuses));
        Assert.That((await db.DiscountCards.SingleAsync()).BonusBalance, Is.EqualTo(100 - bonuses));
        var ledger = await db.BonusTransactions.ToListAsync();
        Assert.That(ledger, Has.Count.EqualTo(bonuses == 0 ? 0 : 1));
        if (bonuses > 0)
        {
            Assert.That(ledger[0].Amount, Is.EqualTo(bonuses));
            Assert.That(ledger[0].Type, Is.EqualTo(TransactionType.Spent));
        }
    }

    [Test, Category("03_Bonuses")]
    [TestCase(101, 1000)]
    [TestCase(51, 50)]
    public async Task Create_InvalidBonuses_IsRejectedWithoutSideEffects(int bonuses, int balance)
    {
        (await db.DiscountCards.SingleAsync()).BonusBalance = balance;
        await db.SaveChangesAsync();
        Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync("customer", 1, bonuses));
        await AssertCheckoutUnchanged(10);
        Assert.That((await db.DiscountCards.SingleAsync()).BonusBalance, Is.EqualTo(balance));
        Assert.That(await db.BonusTransactions.CountAsync(), Is.Zero);
    }

    [Test, Category("04_Cancellation")]
    public async Task Cancel_PendingOrder_RestoresExactBatchesAndBonusesOnlyOnce()
    {
        db.Stocks.Add(new Stock { Id = 2, ProductId = 1, PharmacyId = 1, Quantity = 1, ExpiryDate = Today.AddDays(1) });
        await db.SaveChangesAsync();
        var order = await service.CreateAsync("customer", 1, 50);
        Assert.That(await service.CancelAsync("customer", order.Id), Is.True);
        db.ChangeTracker.Clear();
        Assert.That((await db.Orders.SingleAsync()).Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(await db.Stocks.OrderBy(s => s.Id).Select(s => s.Quantity).ToListAsync(), Is.EqualTo(new[] { 10, 1 }));
        Assert.That((await db.DiscountCards.SingleAsync()).BonusBalance, Is.EqualTo(100));
        var refund = await db.BonusTransactions.SingleAsync(t => t.Type == TransactionType.Earned);
        Assert.That(refund.Amount, Is.EqualTo(50));
        Assert.ThrowsAsync<BusinessException>(() => service.CancelAsync("customer", order.Id));
        db.ChangeTracker.Clear();
        Assert.That((await db.DiscountCards.SingleAsync()).BonusBalance, Is.EqualTo(100));
        Assert.That(await db.BonusTransactions.CountAsync(), Is.EqualTo(2));
    }

    [Test, Category("05_Statuses")]
    public async Task Advance_ValidLifecycle_CompletesOrder()
    {
        var order = await AddOrder(OrderStatus.Pending);
        var before = DateTime.UtcNow;
        foreach (var next in new[] { OrderStatus.Processing, OrderStatus.Ready, OrderStatus.Completed })
        {
            Assert.That(await service.AdvanceAsync("seller", order.Id, next), Is.True);
            db.ChangeTracker.Clear();
            var saved = await db.Orders.SingleAsync();
            Assert.That(saved.Status, Is.EqualTo(next));
            if (next == OrderStatus.Completed)
                Assert.That(saved.CompletedAt, Is.InRange(before, DateTime.UtcNow));
            else Assert.That(saved.CompletedAt, Is.Null);
        }
    }

    [Test, Category("05_Statuses")]
    public async Task Advance_SkippingStatuses_IsRejected()
    {
        var order = await AddOrder(OrderStatus.Pending);
        Assert.ThrowsAsync<BusinessException>(() => service.AdvanceAsync("seller", order.Id, OrderStatus.Completed));
        db.ChangeTracker.Clear();
        Assert.That((await db.Orders.SingleAsync()).Status, Is.EqualTo(OrderStatus.Pending));
    }

    [Test, Category("06_Sales")]
    public async Task Sell_OrdinaryProduct_PersistsPurchaseAndDeductsStock()
    {
        var result = await service.SellAsync("seller", 1, 2, null, null);
        db.ChangeTracker.Clear();
        var purchase = await db.Purchases.Include(p => p.PurchaseItems).SingleAsync();
        Assert.That(purchase.Id, Is.EqualTo(result.Id));
        Assert.That(purchase.TotalCost, Is.EqualTo(200m));
        Assert.That(purchase.PurchaseItems.Single().Quantity, Is.EqualTo(2));
        Assert.That(purchase.PurchaseItems.Single().Price, Is.EqualTo(100m));
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(8));
        Assert.That(await db.PrescriptionOrders.CountAsync(), Is.Zero);
    }

    [Test, Category("06_Sales")]
    public async Task Sell_ValidPrescription_CannotReusePrescription()
    {
        var prescription = await AddPrescription();
        await service.SellAsync("seller", 1, 2, "+79990000000", prescription.Id);
        db.ChangeTracker.Clear();
        var record = await db.PrescriptionOrders.SingleAsync();
        Assert.That(record.Status, Is.EqualTo(PrescriptionOrderStatus.Completed));
        Assert.That(record.TotalCost, Is.EqualTo(200m));
        Assert.That(record.CustomerPhoneNumber, Is.EqualTo("+79990000000"));
        Assert.ThrowsAsync<BusinessException>(() => service.SellAsync("seller", 1, 1, "+79990000000", prescription.Id));
        db.ChangeTracker.Clear();
        Assert.That(await db.Purchases.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(8));
    }

    [Test, Category("06_Sales")]
    public async Task Sell_ExpiredPrescription_IsRejected()
    {
        var prescription = await AddPrescription();
        prescription.ExpiryDate = Today.AddDays(-1);
        await db.SaveChangesAsync();
        Assert.ThrowsAsync<BusinessException>(() => service.SellAsync("seller", 1, 1, "+79990000000", prescription.Id));
        db.ChangeTracker.Clear();
        Assert.That(await db.Purchases.CountAsync(), Is.Zero);
        Assert.That(await db.PrescriptionOrders.CountAsync(), Is.Zero);
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(10));
    }

    [Test, Category("00_CRUD")]
    public async Task Product_CreateReadUpdateDelete_PersistsChanges()
    {
        var product = new Product { Name = "CRUD product", Price = 75m, Category = "Test" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var id = product.Id;
        db.ChangeTracker.Clear();

        var saved = await db.Products.SingleAsync(p => p.Id == id);
        Assert.That(saved.Name, Is.EqualTo("CRUD product"));
        Assert.That(saved.Price, Is.EqualTo(75m));

        saved.Name = "Updated product";
        saved.Price = 90m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        saved = await db.Products.SingleAsync(p => p.Id == id);
        Assert.That(saved.Name, Is.EqualTo("Updated product"));
        Assert.That(saved.Price, Is.EqualTo(90m));

        db.Products.Remove(saved);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.That(await db.Products.AnyAsync(p => p.Id == id), Is.False);
        Assert.That(await db.Products.AnyAsync(p => p.Id == 1), Is.True);
    }

    private async Task<Order> AddOrder(OrderStatus status)
    {
        var order = new Order { CustomerId = "customer", PharmacyId = 1, Status = status, TotalCost = 200m };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private async Task<Prescription> AddPrescription()
    {
        (await db.Products.SingleAsync()).RequiresPrescription = true;
        var prescription = new Prescription { ProductId = 1, SerialNumber = "TEST-001", IssueDate = Today, ExpiryDate = Today };
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();
        return prescription;
    }

    private async Task AssertCheckoutUnchanged(int stockQuantity)
    {
        db.ChangeTracker.Clear();
        Assert.That(await db.Orders.CountAsync(), Is.Zero);
        Assert.That(await db.OrderStocks.CountAsync(), Is.Zero);
        Assert.That(await db.CartItems.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.Stocks.SingleAsync()).Quantity, Is.EqualTo(stockQuantity));
    }
}
