using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad;
using Rally25rs.EFCoreLazyLoad.Specification.Sample.Data;
using Rally25rs.EFCoreLazyLoad.Specification.Sample.Specs;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------------------------------------
// Logging: one line per entry, hide EF Core's chatter, but show the SQL it runs so you can see when (and
// whether) a lazy load hits the database. Lazy-load messages use the "Rally25rs.EFCoreLazyLoad" category.
// ---------------------------------------------------------------------------------------------------------
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information);

// The SQLite database file lives next to the app (in its bin folder) and is recreated on every run.
var connectionString = $"Data Source={Path.Combine(AppContext.BaseDirectory, "shop.db")}";

// ---------------------------------------------------------------------------------------------------------
// The context: UseLazyLoad is all it takes. Specifications and repositories need no changes.
// Load mode with logging: a spec that forgets an Include still works, and the log says which one is missing.
// ---------------------------------------------------------------------------------------------------------
builder.Services.AddDbContext<ShopDbContext>(options => options
    .UseSqlite(connectionString)
    .UseLazyLoad(lazy =>
    {
        lazy.Mode = LazyLoadMode.Load;
        lazy.LogLazyLoads = true;
    }));

// The usual Ardalis.Specification repository registrations.
builder.Services.AddScoped(typeof(IReadRepositoryBase<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(Repository<>));

// A second context in Throw mode, so both modes can be shown in one app. A real app would pick one mode per
// environment, e.g. Throw in development and tests, Load with logging in production.
builder.Services.AddDbContext<StrictShopDbContext>(options => options
    .UseSqlite(connectionString)
    .UseLazyLoad(lazy =>
    {
        lazy.Mode = LazyLoadMode.Throw;
        lazy.LogLazyLoads = true;
    }));

using var host = builder.Build();

Section("Creating and seeding the database");
await CreateDatabaseAsync(host.Services);

// Each demo runs in its own scope, so it gets a fresh context. Sharing one context would let EF's change
// tracker fix up navigations from earlier queries and hide whether they were really loaded.

// ---------------------------------------------------------------------------------------------------------
Section("1. A spec with Include/ThenInclude: everything is loaded up front, no lazy loading");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    var orders = scope.ServiceProvider.GetRequiredService<IReadRepositoryBase<Order>>();

    var order = await orders.SingleOrDefaultAsync(new OrderWithDetailsSpec("SO-1001"))
        ?? throw new InvalidOperationException("Order not found.");

    // One query above; reading the navigations below does not touch the database or log anything.
    PrintOrder(order);
}

// ---------------------------------------------------------------------------------------------------------
Section("2. Load mode, a spec missing Includes: navigations are lazy-loaded on first access, with a warning");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    var orders = scope.ServiceProvider.GetRequiredService<IReadRepositoryBase<Order>>();

    // OrderWithLinesSpec includes the lines, but not the customer or each line's product.
    var order = await orders.SingleOrDefaultAsync(new OrderWithLinesSpec("SO-1001"))
        ?? throw new InvalidOperationException("Order not found.");

    // Without this package, order.Customer and line.Product would be null here. Instead each is loaded on first
    // access (one extra query each, so one per line for the products), and a warning names the navigation, i.e.
    // the Include or ThenInclude the spec is missing.
    PrintOrder(order);
}

// ---------------------------------------------------------------------------------------------------------
Section("3. Throw mode, the same spec: reading a navigation it didn't include throws");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    // A repository over the Throw-mode context. (The DI registrations above use the Load-mode one.)
    var orders = new Repository<Order>(scope.ServiceProvider.GetRequiredService<StrictShopDbContext>());

    var order = await orders.SingleOrDefaultAsync(new OrderWithLinesSpec("SO-1001"))
        ?? throw new InvalidOperationException("Order not found.");

    // The lines were included, so they can be read.
    Print($"Order {order.Number} has {order.Lines.Count} lines");

    try
    {
        Print($"First product: {order.Lines[0].Product.Name}");
    }
    catch (NavigationNotLoadedException ex)
    {
        // No query was run. The exception names the entity and navigation that need an Include.
        Print($"Caught {nameof(NavigationNotLoadedException)} ({ex.EntityType.Name}.{ex.NavigationName}):");
        Print($"  {ex.Message}");
    }
}

// ---------------------------------------------------------------------------------------------------------
Section("4. A projection spec (Select): navigations are read in SQL, so there is nothing to lazy-load");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    // Works the same in either mode; the Throw-mode context shows that nothing is blocked.
    var orders = new Repository<Order>(scope.ServiceProvider.GetRequiredService<StrictShopDbContext>());

    // One query, which joins the customer and aggregates the lines. No entities, no lazy loads, no warnings.
    foreach (var summary in await orders.ListAsync(new OrderSummariesSpec()))
    {
        Print($"{summary.Number} for {summary.CustomerName}: {summary.LineCount} line(s), {summary.TotalQuantity} item(s)");
    }
}

Section("Done");
return;

static void PrintOrder(Order order)
{
    Print($"Order {order.Number} for {order.Customer.Name}");
    foreach (var line in order.Lines)
    {
        Print($"  {line.Quantity} x {line.Product.Name}");
    }
}

// Recreates and seeds the database. EnsureDeleted/EnsureCreated keeps the sample simple; real apps use migrations.
static async Task CreateDatabaseAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();

    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    var widget = new Product { Name = "Widget" };
    var gadget = new Product { Name = "Gadget" };
    db.Customers.Add(new Customer
    {
        Name = "Ada Lovelace",
        Orders =
        [
            new Order
            {
                Number = "SO-1001",
                Lines =
                [
                    new OrderLine { Product = widget, Quantity = 2 },
                    new OrderLine { Product = gadget, Quantity = 1 },
                ],
            },
            new Order
            {
                Number = "SO-1002",
                Lines = [new OrderLine { Product = widget, Quantity = 5 }],
            },
        ],
    });
    await db.SaveChangesAsync();
}

// The console logger writes on a background thread. Short pauses keep its output in order with ours.
static void Section(string title)
{
    Thread.Sleep(100);
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

static void Print(string message)
{
    Thread.Sleep(100);
    Console.WriteLine(message);
}
