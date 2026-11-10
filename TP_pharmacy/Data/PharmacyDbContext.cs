using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TP_pharmacy.Models;

namespace TP_pharmacy.Data;

public class PharmacyDbContext : IdentityDbContext<User>
{
    public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Seller> Sellers { get; set; }
    public DbSet<Manager> Managers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Pharmacy> Pharmacies { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderStock> OrderStocks { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<PurchaseItem> PurchaseItems { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }
    public DbSet<PrescriptionOrder> PrescriptionOrders { get; set; }
    public DbSet<DiscountCard> DiscountCards { get; set; }
    public DbSet<BonusTransaction> BonusTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderStock>().HasOne(s => s.Order).WithMany(o => o.ReservedStocks)
            .HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderStock>().HasOne(s => s.Stock).WithMany()
            .HasForeignKey(s => s.StockId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var fk in entity.GetForeignKeys())
            if (fk.PrincipalEntityType.ClrType == typeof(Product) || fk.PrincipalEntityType.ClrType == typeof(Pharmacy))
                fk.DeleteBehavior = DeleteBehavior.Restrict;

        modelBuilder.Entity<Customer>()
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Seller>()
            .HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<Seller>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Manager>()
            .HasOne(m => m.User)
            .WithOne()
            .HasForeignKey<Manager>(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Stock>()
            .HasIndex(s => new { s.PharmacyId, s.ProductId });

        modelBuilder.Entity<Cart>()
            .HasOne(c => c.Customer)
            .WithOne(cu => cu.Cart)
            .HasForeignKey<Cart>(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscountCard>()
            .HasOne(dc => dc.Customer)
            .WithOne(c => c.DiscountCard)
            .HasForeignKey<DiscountCard>(dc => dc.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Pharmacy)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Purchase>()
            .HasOne(p => p.Seller)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PrescriptionOrder>()
            .HasOne(po => po.Seller)
            .WithMany(s => s.PrescriptionOrders)
            .HasForeignKey(po => po.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Category);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Name);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status);

        modelBuilder.Entity<Prescription>()
            .HasIndex(p => p.SerialNumber)
            .IsUnique();

        modelBuilder.Entity<PrescriptionOrder>().HasOne(p => p.Prescription).WithMany(p => p.PrescriptionOrders)
            .HasForeignKey(p => p.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Stock>().Property(s => s.Quantity).IsConcurrencyToken();
        modelBuilder.Entity<Order>().Property(o => o.Status).IsConcurrencyToken();
        modelBuilder.Entity<DiscountCard>().Property(c => c.BonusBalance).IsConcurrencyToken();
    }
}
