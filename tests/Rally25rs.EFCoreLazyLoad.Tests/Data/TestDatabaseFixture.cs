using Microsoft.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

/// <summary>
/// Creates and seeds a real SQLite database file (kept inside the repo under
/// tests/.../TestDb/) once per test class. The file is recreated on every run.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    public const int SeededCustomerId = 1;

    private readonly DbContextOptions<TestDbContext> _options;

    public string DatabasePath { get; }

    public TestDatabaseFixture()
    {
        var dir = Path.Combine(FindTestProjectDirectory(), "TestDb");
        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "test.db");

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        Seed(context);
    }

    /// <summary>Creates a new context, so each test gets a clean change tracker.</summary>
    public TestDbContext CreateContext() => new(_options);

    private static void Seed(TestDbContext context)
    {
        context.Customers.Add(new Customer
        {
            Id = SeededCustomerId,
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                PostalCode = "12345"
            },
            Hobbies =
            [
                new Hobby { Name = "Cycling" },
                new Hobby { Name = "Chess" },
                new Hobby { Name = "Photography" }
            ]
        });
        context.SaveChanges();
    }

    private static string FindTestProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.csproj").Length == 0)
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the test project directory.");
    }

    public void Dispose()
    {
        // Release pooled SQLite connections so the file isn't locked.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }
}
