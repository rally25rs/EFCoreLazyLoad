using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad;
using Rally25rs.EFCoreLazyLoad.Sample.Data;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------------------------------------
// Logging: the host already logs to the console. Keep it to one line per entry, hide EF Core's chatter,
// but show the SQL it runs so you can see when (and whether) a lazy load hits the database.
// Lazy-load messages are written under the "Rally25rs.EFCoreLazyLoad" category at Warning level.
// ---------------------------------------------------------------------------------------------------------
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information);

// The SQLite database file lives next to the app (in its bin folder) and is recreated on every run.
var connectionString = $"Data Source={Path.Combine(AppContext.BaseDirectory, "blog.db")}";

// ---------------------------------------------------------------------------------------------------------
// Option A (main path): configure the lazy-load options inline with UseLazyLoad(...).
// ---------------------------------------------------------------------------------------------------------
builder.Services.AddDbContext<BlogDbContext>(options => options
    .UseSqlite(connectionString)
    .UseLazyLoad(lazy =>
    {
        lazy.Mode = LazyLoadMode.Load;  // load unloaded navigations on first access
        lazy.LogLazyLoads = true;       // ...and log a warning each time, so missing Includes can be found
    }));

// ---------------------------------------------------------------------------------------------------------
// Option B: register the options once with AddEFCoreLazyLoad(...), and apply them with UseLazyLoad(sp).
// Handy when several contexts share the settings, or to pick them per environment, e.g.
//     lazy.Mode = builder.Environment.IsDevelopment() ? LazyLoadMode.Throw : LazyLoadMode.Load;
// Here it configures a second context in Throw mode so both modes can be shown in one app.
// ---------------------------------------------------------------------------------------------------------
builder.Services.AddEFCoreLazyLoad(lazy =>
{
    lazy.Mode = LazyLoadMode.Throw;     // throw NavigationNotLoadedException instead of querying
    lazy.LogLazyLoads = true;
});
builder.Services.AddDbContext<StrictBlogDbContext>((serviceProvider, options) => options
    .UseSqlite(connectionString)
    .UseLazyLoad(serviceProvider));

using var host = builder.Build();

Section("Creating and seeding the database");
await CreateDatabaseAsync(host.Services);

// Each demo runs in its own scope, so it gets a fresh context. Sharing one context would let EF's change
// tracker fix up navigations from earlier queries and hide whether they were really loaded.

// ---------------------------------------------------------------------------------------------------------
Section("1. Query with Include: everything is loaded up front, no lazy loading");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

    var blog = await db.Blogs
        .Include(b => b.Author)
        .Include(b => b.Posts)
        .SingleAsync(b => b.Title == "Lazy Thoughts");

    // One query above; reading the navigations below does not touch the database or log anything.
    Print($"'{blog.Title}' by {blog.Author.Name}, {blog.Posts.Count} posts");
}

// ---------------------------------------------------------------------------------------------------------
Section("2. Load mode, no Include: navigations are lazy-loaded on first access, with a warning");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

    var blog = await db.Blogs.SingleAsync(b => b.Title == "Lazy Thoughts");  // no Include

    // Without this package, blog.Author would be null here. Instead it is loaded now (one extra query each),
    // and a warning names the navigation, i.e. the Include that the query above is missing.
    Print($"Author: {blog.Author.Name}");
    Print($"Posts:  {string.Join(", ", blog.Posts.Select(p => p.Title))}");

    // Already loaded navigations are never queried (or logged) again.
    Print($"Author again: {blog.Author.Name}");
}

// ---------------------------------------------------------------------------------------------------------
Section("3. Throw mode, no Include: accessing an unloaded navigation throws");
// ---------------------------------------------------------------------------------------------------------
await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StrictBlogDbContext>();

    var blog = await db.Blogs.SingleAsync(b => b.Title == "Lazy Thoughts");  // no Include

    try
    {
        Print($"Author: {blog.Author.Name}");
    }
    catch (NavigationNotLoadedException ex)
    {
        // No query was run. The exception names the entity and navigation that need an Include.
        Print($"Caught {nameof(NavigationNotLoadedException)} ({ex.EntityType.Name}.{ex.NavigationName}):");
        Print($"  {ex.Message}");
    }
}

Section("Done");
return;

// Recreates and seeds the database. EnsureDeleted/EnsureCreated keeps the sample simple; real apps use migrations.
static async Task CreateDatabaseAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    db.Blogs.Add(new Blog
    {
        Title = "Lazy Thoughts",
        Author = new Author { Name = "Ada Lovelace" },
        Posts =
        [
            new Post { Title = "Hello, world" },
            new Post { Title = "On partial properties" },
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
