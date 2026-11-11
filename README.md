# Rally25rs.EFCoreLazyLoad

An extension for Entity Framework Core that lets you **optionally lazy-load entity relations**, and helps you **find places where a relation was accessed without being loaded**.

## Why

EF Core leaves a navigation property as `null` unless you explicitly load it with `Include`. Your entity classes usually declare those navigations as non-nullable (`public Address Address { get; set; }`), so the compiler and IntelliSense assume they always have a value. Forget an `Include` and you get a `NullReferenceException` at runtime, often far away from the query that caused it.

This package makes those navigations behave the way their types say they do:

- **Lazy-load mode**: an unloaded navigation is loaded from the database the first time you read it, so it is never unexpectedly `null`.
- **Throw mode**: reading an unloaded navigation throws a clear `NavigationNotLoadedException` that names the entity and navigation, instead of returning `null`.
- **Logging**: optionally log every time a navigation had to be lazy-loaded (or was blocked), so you can track down and add the missing `Include`s.

## Requirements

- .NET 10 (C# 14)
- Entity Framework Core 10

## Getting started

### 1. Install the package

```shell
dotnet add package Rally25rs.EFCoreLazyLoad
```

### 2. Mark your entities

Add `[LazyLoad]` to the entity class, make the class `partial`, and declare its navigation properties as `partial` without a body. The package implements them for you.

```csharp
using Rally25rs.EFCoreLazyLoad;

[LazyLoad]
public partial class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public partial Address Address { get; set; }        // one-to-one
    public partial List<Hobby> Hobbies { get; set; }    // one-to-many
}

[LazyLoad]
public partial class Hobby
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string Name { get; set; }

    public partial Customer Customer { get; set; }
}
```

Regular properties in the class are left alone. To opt in only some navigations, put `[LazyLoad]` on those properties instead of on the class (the class must still be `partial`).

You can also override the context's settings for a single entity or navigation. See [Per-entity settings](#per-entity-settings).

### 3. Enable it on your DbContext

```csharp
using Rally25rs.EFCoreLazyLoad;

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseLazyLoad(lazy =>
    {
        lazy.Mode = LazyLoadMode.Load;  // or LazyLoadMode.Throw
        lazy.LogLazyLoads = true;
    }));
```

It also works with `AddDbContextPool` and with contexts you configure in `OnConfiguring`.

### 4. Use your entities

```csharp
var customer = await db.Customers.SingleAsync(c => c.Id == id); // no Include

var city = customer.Address.City; // Load mode: Address is loaded here, not null
```

## Configuration

| Option | Default | Description |
|---|---|---|
| `Mode` | `LazyLoadMode.Load` | `Load`: load unloaded navigations from the database on first access. `Throw`: throw `NavigationNotLoadedException` instead, without querying the database. |
| `LogLazyLoads` | `false` | Log a message each time a navigation is lazy-loaded (or blocked in `Throw` mode). |
| `LogLevel` | `LogLevel.Warning` | The level those messages are logged at. |
| `LoggerFactory` | `null` | The logger factory to write to. When not set, the application's `ILoggerFactory` is used (the one registered in your service collection when you use `AddDbContext`). |

### Configuring through dependency injection

You can register the options once in your service collection, and apply them to one or more contexts:

```csharp
builder.Services.AddEFCoreLazyLoad(lazy =>
{
    lazy.Mode = builder.Environment.IsDevelopment() ? LazyLoadMode.Throw : LazyLoadMode.Load;
    lazy.LogLazyLoads = true;
});

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => options
    .UseSqlServer(connectionString)
    .UseLazyLoad(serviceProvider));
```

A common setup is `Throw` in development and tests, so missing `Include`s fail fast, and `Load` with logging in production, so users never see an error while you collect the missing `Include`s from your logs.

### Per-entity settings

`[LazyLoad]` accepts the `Mode`, `LogLazyLoads` and `LogLevel` settings, which override the context's configuration for that entity or navigation:

```csharp
// Orders must always be queried with their lines: never lazy-load them, even in Load mode.
[LazyLoad(Mode = LazyLoadMode.Throw)]
public partial class Order
{
    public int Id { get; set; }
    public partial List<OrderLine> Lines { get; set; }

    // ...except the customer, which may be lazy-loaded, and is logged so the missing Include can be found.
    [LazyLoad(Mode = LazyLoadMode.Load, LogLazyLoads = true)]
    public partial Customer Customer { get; set; }
}
```

- Only the settings you set are overridden. Anything you leave out follows the context's configuration. For example, `[LazyLoad(LogLazyLoads = true)]` logs that entity's lazy loads but keeps the context's `Mode`.
- Each setting is decided separately. A setting on the navigation property wins over one on the class, and a setting on a class wins over one on its base classes, which wins over the context's configuration.
- Settings on a base class apply to derived entity classes too, unless the derived class overrides them.
- Per-entity settings are only applied when the context uses `UseLazyLoad`. `LoggerFactory` can only be set on the context.

### Logging

Messages are written under the logger category `Rally25rs.EFCoreLazyLoad`:

| Event ID | Name | When |
|---|---|---|
| 1 | `LazyLoad` | A navigation was lazy-loaded (`Load` mode). |
| 2 | `LazyLoadBlocked` | A lazy load was blocked and an exception thrown (`Throw` mode). |

Each message names the entity type and navigation, for example `Lazy-loading navigation 'Customer.Address'`, so you know which `Include` to add.

If you create your `DbContext` with `new` rather than through dependency injection, set `LoggerFactory` (or call EF Core's `UseLoggerFactory`) so the messages have somewhere to go.

## Using with Ardalis.Specification

The package works with [Ardalis.Specification](https://specification.ardalis.com/) (`Ardalis.Specification` and `Ardalis.Specification.EntityFrameworkCore`, version 9.x, which runs on EF Core 10). Nothing about your specifications or repositories changes: enable `UseLazyLoad` on the context, and every entity a specification returns gets lazy loading, `Throw` mode and logging.

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseLazyLoad(lazy =>
    {
        lazy.Mode = LazyLoadMode.Load;
        lazy.LogLazyLoads = true;
    }));

builder.Services.AddScoped(typeof(IReadRepositoryBase<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(Repository<>));

public class Repository<T>(AppDbContext db) : RepositoryBase<T>(db), IRepositoryBase<T>
    where T : class;
```

A specification's `Include` and `ThenInclude` work as usual. When a specification forgets one, reading that navigation follows the context's mode:

```csharp
public sealed class OrderWithLinesSpec : SingleResultSpecification<Order>
{
    public OrderWithLinesSpec(int id)
        => Query
            .Where(o => o.Id == id)
            .Include(o => o.Lines);   // no .ThenInclude(l => l.Product)
}

var order = await repository.SingleOrDefaultAsync(new OrderWithLinesSpec(id));

var lines = order!.Lines;           // included: returned as-is, no query
var name = lines[0].Product.Name;   // Load mode: loaded now, and logged as 'OrderLine.Product'
                                    // Throw mode: throws NavigationNotLoadedException
```

- **Find the missing Includes.** With `LogLazyLoads` on, each lazy load is logged with the entity and navigation it loaded, so you know which specification needs an `Include` or `ThenInclude`. In `Throw` mode, the exception names it too. A specification that returns a list and is missing an `Include` lazy-loads once per entity, so the log shows one message per entity.
- **No-tracking specifications** (`Query.AsNoTracking()` and `Query.AsNoTrackingWithIdentityResolution()`) behave the same way.
- **Projections** (`Specification<T, TResult>` with `Query.Select(...)`) need no `Include`s. Navigations you read inside `Select` are part of the query, so nothing is lazy-loaded or blocked. If a projection returns entities (for example `Select(o => o.Customer)`), those entities lazy-load or throw like any other.
- **Per-entity settings** from `[LazyLoad(...)]` apply to entities returned by specifications, too.
- **`WithSpecification`** on a `DbSet` or `IQueryable` behaves the same as the repository methods.

## Behavior notes

- **Lazy loading is synchronous.** Reading a navigation property that needs loading runs a blocking database query. This is the same as EF Core's own built-in [lazy loading](https://learn.microsoft.com/en-us/ef/core/querying/related-data/lazy), which this package builds on. Property getters can't be awaited, so neither implementation can load asynchronously. It keeps your code simple, but loading inside a loop issues one query per item (the "N+1" problem), which Microsoft warns about in [Beware of lazy loading](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying#beware-of-lazy-loading). Use the log messages to find these spots and replace them with `Include`.
- **Loaded navigations are never re-queried.** Navigations loaded with `Include`, loaded explicitly, or already lazy-loaded are returned directly.
- **Empty collections** are loaded as an empty list, not `null`.
- **No-tracking queries** (`AsNoTracking`, `AsNoTrackingWithIdentityResolution`) are supported.
- **The context must still be alive.** Reading an unloaded navigation after its `DbContext` was disposed (or returned to the pool) throws EF Core's standard `InvalidOperationException`.
- **Turning it off temporarily:** setting `context.ChangeTracker.LazyLoadingEnabled = false` disables lazy loading and `Throw` mode for that context. Unloaded navigations then return `null`.
- **Without `UseLazyLoad`**, entities marked with `[LazyLoad]` fall back to EF Core's built-in lazy loading: they lazy-load, but you get no `Throw` mode and none of this package's logging.
- **Serialization:** the lazy-loading plumbing added to your entities is ignored by `System.Text.Json`. Serializing an entity reads its navigations, which lazy-loads them (or throws in `Throw` mode), so consider serializing DTOs rather than entities.

## Compiler diagnostics

The package reports build errors when an entity is not set up correctly:

| ID | Problem |
|---|---|
| EFLL001 | A `[LazyLoad]` class (or a class containing it) is not `partial`. |
| EFLL002 | A `[LazyLoad]` property is not `partial`, or is not a declaration without a body. |
| EFLL003 | A `[LazyLoad]` property already has an implementation. |
| EFLL004 | A `[LazyLoad]` property is a value type. Only navigations to entities and collections can be lazy-loaded. |
| EFLL005 | A `[LazyLoad]` property has no getter. |
| EFLL006 | A `[LazyLoad]` property is static or an indexer. |
| EFLL007 | A base class has a lazy-loader member that is not accessible from the derived class. |
| EFLL008 | The project's C# language version is older than 14. |
