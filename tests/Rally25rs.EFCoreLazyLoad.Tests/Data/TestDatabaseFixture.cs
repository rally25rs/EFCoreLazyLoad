using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rally25rs.EFCoreLazyLoad.Tests.Data;
using Rally25rs.EFCoreLazyLoad.Tests.Support;

// One database for the whole test assembly: it is created and seeded once, before any test runs, and test
// classes still run in parallel. Tests only read from it, so sharing it is safe.
[assembly: AssemblyFixture(typeof(TestDatabaseFixture))]

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

/// <summary>
///     Creates and seeds a real SQLite database file (kept inside the repo under tests/.../TestDb/) once per test
///     run, and builds per-test <see cref="TestHost" />s against it.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    /// <summary>Has an address and three hobbies.</summary>
    public const int CustomerWithHobbiesId = 1;

    /// <summary>Has an address and no hobbies.</summary>
    public const int CustomerWithoutHobbiesId = 2;

    /// <summary>A <see cref="Supplier" /> with a contact and two products.</summary>
    public const int SupplierId = 1;

    /// <summary>An <see cref="Order" /> (DDD-style read-only collection) with two lines.</summary>
    public const int OrderWithLinesId = 1;

    /// <summary>An <see cref="Order" /> with no lines.</summary>
    public const int OrderWithoutLinesId = 2;

    public TestDatabaseFixture()
    {
        var dir = Path.Combine(FindTestProjectDirectory(), "TestDb");
        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "test.db");
        ConnectionString = $"Data Source={DatabasePath}";

        using var context = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>().UseSqlite(ConnectionString).Options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        Seed(context);
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    /// <summary>
    ///     A host whose context is registered with <c>AddDbContext</c> and <c>UseLazyLoad(configure)</c>.
    /// </summary>
    public TestHost CreateHost(Action<LazyLoadOptions>? configure = null)
        => CreateHost((services, useDatabase) => services.AddDbContext<TestDbContext>(o =>
        {
            useDatabase(o);
            o.UseLazyLoad(configure);
        }));

    /// <summary>
    ///     A host whose context is registered with <c>AddDbContextPool</c> and <c>UseLazyLoad(configure)</c>.
    /// </summary>
    public TestHost CreatePooledHost(Action<LazyLoadOptions>? configure = null)
        => CreateHost((services, useDatabase) => services.AddDbContextPool<TestDbContext>(o =>
        {
            useDatabase(o);
            o.UseLazyLoad(configure);
        }));

    /// <summary>
    ///     A host with custom registrations. <paramref name="register" /> must register <see cref="TestDbContext" />
    ///     and call the supplied <c>useDatabase</c> action on its options, which points it at the test database and
    ///     adds the host's SQL-capturing interceptor.
    /// </summary>
    public TestHost CreateHost(Action<IServiceCollection, Action<DbContextOptionsBuilder>> register)
        => new(ConnectionString, register);

    private static void Seed(TestDbContext context)
    {
        context.Customers.Add(new Customer
        {
            Id = CustomerWithHobbiesId,
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                PostalCode = "12345",
            },
            Hobbies =
            [
                new Hobby { Name = "Cycling" },
                new Hobby { Name = "Chess" },
                new Hobby { Name = "Photography" },
            ],
        });
        context.Customers.Add(new Customer
        {
            Id = CustomerWithoutHobbiesId,
            Name = "John Roe",
            Email = "john.roe@example.com",
            Address = new Address
            {
                Street = "9 Elm Rd",
                City = "Shelbyville",
                PostalCode = "67890",
            },
        });
        context.Suppliers.Add(new Supplier
        {
            Id = SupplierId,
            Name = "Acme",
            Contact = new Contact { Email = "sales@acme.example" },
            Products =
            [
                new Product { Name = "Anvil" },
                new Product { Name = "Rocket Skates" },
            ],
        });
        var order = new Order { Id = OrderWithLinesId, Reference = "ORD-1" };
        order.AddLine("Widget", 2);
        order.AddLine("Gadget", 1);
        context.Orders.Add(order);
        context.Orders.Add(new Order { Id = OrderWithoutLinesId, Reference = "ORD-2" });
        context.SaveChanges();
    }

    private static string FindTestProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.csproj").Length == 0)
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the test project directory.");
    }

    public void Dispose()
        // Release pooled SQLite connections so the file isn't locked.
        => SqliteConnection.ClearAllPools();
}
