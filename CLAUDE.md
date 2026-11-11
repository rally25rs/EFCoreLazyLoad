# Rally25rs.EFCoreLazyLoad

## Purpose

This package extends EF Core to add the ability to **optionally lazy-load entity relations**. It can also be used to **detect and log, or throw an error, when a relation needed to be lazy-loaded** (i.e. it was accessed without having been loaded up front).

Namespace: `Rally25rs.EFCoreLazyLoad`

## Architecture

Two halves, joined by EF Core's own `ILazyLoader` contract (the same one EF's built-in proxy-less lazy loading uses):

1. **Source generator (entity side)**: `src/Rally25rs.EFCoreLazyLoad.Generators/`. For `[LazyLoad] partial` classes it implements each body-less `partial` property:
   - a `protected` (or `private` if the class is sealed) `[JsonIgnore] ILazyLoader? __lazyLoader` field, one per hierarchy (derived types reuse the base's);
   - getters calling `LazyLoaderExtensions.Load(__lazyLoader, this, ref field, nameof(X))!` using the C# 14 `field` keyword.

   EF's `ServicePropertyDiscoveryConvention` finds the `ILazyLoader` member and injects a loader per entity, tracking or no-tracking. It also forces `PropertyAccessMode.Field` on the navigations, so EF never trips the lazy getters itself.

   The member type must be exactly `ILazyLoader`. EF's `IsLoaded` only consults the entity's own loader for that exact type, and no-tracking entities depend on it.
2. **Runtime (DbContext side)**: `src/Rally25rs.EFCoreLazyLoad/`.
   - `UseLazyLoad(...)` adds `LazyLoadOptionsExtension` (an `IDbContextOptionsExtension`). Its `ApplyServices` replaces EF's **`ILazyLoaderFactory`** with `LazyLoadLazyLoaderFactory`. Replace the factory, not `ILazyLoader`: the factory disposes loaders on context dispose or pool return.
   - `LazyLoadLazyLoader` subclasses EF's `LazyLoader`. It pre-checks whether EF would actually load (mirroring EF's private `ShouldLoad`), then logs and/or throws `NavigationNotLoadedException`, and otherwise defers to `base.Load`. The base keeps EF's reentrancy guard, no-tracking support and disposed-context handling.
   - The extension's `Info` reports the same service provider for any settings, and the factory reads the options per context. Changing options therefore never builds new internal service providers.
   - Logging goes to `options.LoggerFactory` if set, otherwise the `ILoggerFactory` from EF's internal provider (the app's, via `AddDbContext`). The category is `Rally25rs.EFCoreLazyLoad`; EventIds are 1 `LazyLoad` and 2 `LazyLoadBlocked`.

EF lazy loading is **synchronous blocking I/O** (`NavigationEntry.Load()`), not sync-over-async. We inherit that; there is no async masking.

Rejected alternatives:
- Castle proxies need virtual navigations, add a dependency, and don't work with AOT.
- C# interceptors can't intercept property gets.
- A materialization interceptor would duplicate what EF's service-property injection already does.

## Repo layout

- `src/Rally25rs.EFCoreLazyLoad/`: the runtime library (net10.0). It also packs the generator dll into the nupkg's `analyzers/dotnet/cs` (the `_PackSourceGenerator` target in its csproj).
- `src/Rally25rs.EFCoreLazyLoad.Generators/`: the incremental source generator (netstandard2.0, Roslyn 4.14). It matches the attribute by metadata name and does not reference the runtime lib. Diagnostics are EFLL001–EFLL008, tracked in `AnalyzerReleases.*.md`.
- `tests/Rally25rs.EFCoreLazyLoad.Tests/`: xUnit v3 integration tests against a real SQLite database. It references the generator as an analyzer (`OutputItemType="Analyzer"`).
  - `Data/`: test entities (`Customer` 1-to-1 `Address`, `Customer` 1-to-many `Hobby`, all `[LazyLoad] partial` with non-nullable navigations), `TestDbContext`, and `TestDatabaseFixture`.
  - `Support/`: `TestHost` (a fresh DI container and context per test), `SqlCapture` (a command interceptor; `During(action)` returns the SQL run inside it), and `LogCapture`.
- `tests/Rally25rs.EFCoreLazyLoad.Generators.Tests/`: generator unit tests via `CSharpGeneratorDriver` (output, diagnostics, hierarchy, incremental caching).
- `README.md`: consumer-facing documentation only (see below).

## Commands

- Build: `dotnet build` (must be 0 warnings; src projects treat warnings as errors)
- Test: `dotnet test`
- Pack: `dotnet pack src/Rally25rs.EFCoreLazyLoad -o <dir>`

## Conventions

- **README.md is consumer-facing only.** It covers the purpose and usage of the package. Do not put implementation details, technical decisions, or contributor notes in it; those belong in this file or in code comments. Keep it in sync when the public API, options, log events or diagnostics change.
- **Target EF Core 10.x**, pinned to `[10.0.15, 11.0)`. EF Core 11 targets net11 only and will not restore on net10.0. The runtime lib uses EF internal types (`LazyLoader`, `ILazyLoaderFactory`), so EF1001 is suppressed in that project only. Re-verify these types when moving to a new EF major version.
- **Keep the public surface minimal.** Public types: `LazyLoadAttribute`, `LazyLoadMode`, `LazyLoadOptions`, `NavigationNotLoadedException`, the `UseLazyLoad` overloads, and `AddEFCoreLazyLoad`. Everything else is `internal`, under `Rally25rs.EFCoreLazyLoad.Internal`.
- **Tests use a real database, not the in-memory provider.** The library's behavior depends on the actual SQL EF Core generates, so tests assert on captured SQL. `TestDatabaseFixture` is an xUnit v3 **assembly fixture**: it recreates and seeds a SQLite file at `tests/Rally25rs.EFCoreLazyLoad.Tests/TestDb/test.db` (gitignored) once per test run. Tests only read, so test classes run in parallel.
- **Each test creates its own host and context** via `db.CreateHost(...)` / `CreatePooledHost(...)`. Sharing a context would let the change tracker fix up relations and hide whether they were really loaded.
- Test entity navigations are non-nullable `partial` properties on purpose: the library makes the non-nullable declaration truthful. The baseline test shows raw EF behavior (null) by setting `ChangeTracker.LazyLoadingEnabled = false`.
- Pass `TestContext.Current.CancellationToken` to async calls in tests (xUnit1051).
