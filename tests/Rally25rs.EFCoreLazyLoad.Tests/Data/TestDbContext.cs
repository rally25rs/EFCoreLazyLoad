using Microsoft.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Hobby> Hobbies => Set<Hobby>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1-to-1: Customer <-> Address (Address holds the FK)
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.Address)
            .WithOne(a => a.Customer)
            .HasForeignKey<Address>(a => a.CustomerId);

        // 1-to-many: Customer -> Hobbies
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Hobbies)
            .WithOne(h => h.Customer)
            .HasForeignKey(h => h.CustomerId);

        // Entities with [LazyLoad(...)] overrides: Supplier 1-to-1 Contact, Supplier 1-to-many Products
        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Contact)
            .WithOne(c => c.Supplier)
            .HasForeignKey<Contact>(c => c.SupplierId);

        modelBuilder.Entity<Supplier>()
            .HasMany(s => s.Products)
            .WithOne(p => p.Supplier)
            .HasForeignKey(p => p.SupplierId);

        // DDD-style read-only collection: Order -> Lines, mapped to the private `_lines` field by convention.
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId);

        // A hand-written lazy-loading getter (Parcels, over `_parcels`) and a nullable backing field (Labels, over
        // `_labels`), both mapped by convention.
        modelBuilder.Entity<Shipment>()
            .HasMany(s => s.Parcels)
            .WithOne(p => p.Shipment)
            .HasForeignKey(p => p.ShipmentId);

        modelBuilder.Entity<Shipment>()
            .HasMany(s => s.Labels)
            .WithOne()
            .HasForeignKey(l => l.ShipmentId);
    }
}
