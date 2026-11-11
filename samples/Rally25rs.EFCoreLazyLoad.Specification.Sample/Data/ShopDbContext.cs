using Microsoft.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Specification.Sample.Data;

// A plain EF Core context: nothing here is specific to Rally25rs.EFCoreLazyLoad or Ardalis.Specification.
// Lazy loading is switched on where the context is registered (see Program.cs).
public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options)
        : base(options)
    {
    }

    // Lets a derived context (StrictShopDbContext) pass its own options type through.
    protected ShopDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId);

        modelBuilder.Entity<OrderLine>()
            .HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId);
    }
}

// The same model, registered a second time with different lazy-load options (Throw mode) so the sample can
// show both modes side by side. A real app would normally have just one registration.
public class StrictShopDbContext(DbContextOptions<StrictShopDbContext> options) : ShopDbContext(options);
