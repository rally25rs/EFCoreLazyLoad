using Microsoft.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Hobby> Hobbies => Set<Hobby>();

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
    }
}
