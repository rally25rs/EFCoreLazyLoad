# Rally25rs.EFCoreLazyLoad with Ardalis.Specification sample

A small console app showing Rally25rs.EFCoreLazyLoad together with
[Ardalis.Specification](https://specification.ardalis.com/): specifications, a `RepositoryBase<T>` repository,
the .NET Generic Host, and a SQLite database.

## Run it

From the repository root:

```shell
dotnet run --project samples/Rally25rs.EFCoreLazyLoad.Specification.Sample
```

The app recreates and seeds `shop.db` in its output folder on every run, so it can be run repeatedly.

## What's in it

- `Data/`: a `Customer` with many `Order`s, each with many `OrderLine`s, each pointing to a `Product`. Each entity
  is a `[LazyLoad] partial` class with non-nullable `partial` navigation properties. `ShopDbContext` is a plain
  EF Core context, and `Repository<T>` is a plain Ardalis.Specification `RepositoryBase<T>`.
- `Specs/`: the specifications.
  - `OrderWithDetailsSpec` includes everything: `.Include(o => o.Customer)` and
    `.Include(o => o.Lines).ThenInclude(l => l.Product)`.
  - `OrderWithLinesSpec` includes only the lines, so the customer and each line's product are missing.
  - `OrderSummariesSpec` projects orders to an `OrderSummary` record with `Select`.
- `Program.cs`: registers `ShopDbContext` with `UseLazyLoad(...)` in `Load` mode with logging, and the repository
  as `IReadRepositoryBase<>` / `IRepositoryBase<>`. A second context, `StrictShopDbContext`, uses `Throw` mode so
  both modes can be shown in one app.

## Expected output

Console logging shows the SQL EF Core runs and the warnings from the `Rally25rs.EFCoreLazyLoad` category.

1. **Spec with Include/ThenInclude**: one query loads the order, customer, lines and products. No lazy loading,
   no warnings.
2. **Load mode, spec missing Includes**: the order and lines are queried together. Reading `order.Customer` logs
   `Lazy-loading navigation 'Order.Customer'` and runs one extra query; reading each `line.Product` does the same,
   once per line (the N+1 pattern the warnings help you find).
3. **Throw mode, same spec**: the included lines can be read. Reading `line.Product` logs
   `Blocked lazy-load of navigation 'OrderLine.Product'` and throws a `NavigationNotLoadedException`, without
   querying.
4. **Projection spec**: one query, which joins the customer and aggregates the lines. No entities are loaded, so
   nothing is lazy-loaded or blocked, even in `Throw` mode.
