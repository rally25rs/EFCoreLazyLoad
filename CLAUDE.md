# Rally25rs.EFCoreLazyLoad

## Purpose

This package extends EF Core to add the ability to **optionally lazy-load entity relations**. It can also be used to **detect and log, or throw an error, when a relation needed to be lazy-loaded** (i.e. it was accessed without having been loaded up front).

Namespace: `Rally25rs.EFCoreLazyLoad`

## Architecture

Two halves, joined by EF Core's own `ILazyLoader` contract (the same one EF's built-in proxy-less lazy loading uses):

1. **Source generator (entity side)**: `src/Rally25rs.EFCoreLazyLoad.Generators/`. For `[LazyLoad] partial` classes it implements each body-less `partial` property:
   - a `protected` (or `private` if the class is sealed) `[JsonIgnore] ILazyLoader? __lazyLoader` field, one per hierarchy (derived types reuse the base's);
   - getters calling `LazyLoaderExtensions.Load(__lazyLoader, this, ref field, nameof(X))!` using the C# 14 `field` keyword.

   - **Get-only partials over a declared backing field** (the DDD `private readonly List<T> _items` / `IEnumerable<T> Items { get; }` pattern) don't use `field`. The generator finds the field by EF's backing-field naming convention (`_camel`, `_Pascal`, `m_camel`, `m_Pascal`, `camel`, on the declaring type only). It emits `get { __lazyLoader?.Load(this, nameof(X)); return this._items.AsReadOnly(); }`, using `.AsReadOnly()` when the field type has one that converts to the property type, otherwise returning the field directly (EFLL009 if neither converts). It must not touch `field`, because EF's convention prefers a compiler-generated backing field over a convention-named one. EF maps the navigation to the declared field and fills it (it adds into a pre-initialized list). Properties with a setter, or with no matching field, keep the `field`-based getter. EFLL010 (warning) flags the hand-written version of the pattern in a class-level `[LazyLoad]` class, which silently never lazy-loads. It is limited to collections of class types other than `string` to avoid flagging value objects. Tested by `ReadOnlyCollectionTests` (integration, `Order`/`OrderLine` entities) and `BackingFieldTests` (generator).

   EF's `ServicePropertyDiscoveryConvention` finds the `ILazyLoader` member and injects a loader per entity, tracking or no-tracking. It also forces `PropertyAccessMode.Field` on the navigations, so EF never trips the lazy getters itself.

   The member type must be exactly `ILazyLoader`. EF's `IsLoaded` only consults the entity's own loader for that exact type, and no-tracking entities depend on it.
2. **Runtime (DbContext side)**: `src/Rally25rs.EFCoreLazyLoad/`.
   - `UseLazyLoad(...)` adds `LazyLoadOptionsExtension` (an `IDbContextOptionsExtension`). Its `ApplyServices` replaces EF's **`ILazyLoaderFactory`** with `LazyLoadLazyLoaderFactory`. Replace the factory, not `ILazyLoader`: the factory disposes loaders on context dispose or pool return.
   - `LazyLoadLazyLoader` subclasses EF's `LazyLoader`. It pre-checks whether EF would actually load (mirroring EF's private `ShouldLoad`), then logs and/or throws `NavigationNotLoadedException`, and otherwise defers to `base.Load`. The base keeps EF's reentrancy guard, no-tracking support and disposed-context handling.
   - **Per-entity overrides:** `[LazyLoad(Mode = ..., LogLazyLoads = ..., LogLevel = ...)]` overrides the context options. The attribute tracks which settings were explicitly set (nullable backing fields, exposed internally as `*Override`), so an unset setting falls through. `Internal/NavigationSettings.cs` resolves each setting independently, cached per (CLR type, navigation): property attribute → runtime type's class attribute → base classes' attributes → context options (`EffectiveSettings.From`). It works by reflection over the CLR type, not the EF model, so it also applies to proxy subclasses. The generator ignores the attribute's arguments.
   - **Stack traces in logs:** when `LogStackTrace` is on (default) and the logger is enabled at the level, `Internal/CallStack.cs` captures `new StackTrace(fNeedFileInfo: true)`. It drops the leading frames from `Microsoft.EntityFrameworkCore.*`, `Rally25rs.EFCoreLazyLoad.Internal.*` and `Castle.*` by text prefix, so the trace starts at the generated getter. The trace is attached as the log entry's exception (`LazyLoadStackTrace`, never thrown, overrides `StackTrace`), so providers render it natively. This is covered by `StackTraceLoggingTests`, which also pass in Release.
   - The extension's `Info` reports the same service provider for any settings, and the factory reads the options per context. Changing options therefore never builds new internal service providers.
   - Logging goes to `options.LoggerFactory` if set, otherwise the `ILoggerFactory` from EF's internal provider (the app's, via `AddDbContext`). The category is `Rally25rs.EFCoreLazyLoad`; EventIds are 1 `LazyLoad` and 2 `LazyLoadBlocked`.

EF lazy loading is **synchronous blocking I/O** (`NavigationEntry.Load()`), not sync-over-async. We inherit that; there is no async masking.

Rejected alternatives:
- Castle proxies need virtual navigations, add a dependency, and don't work with AOT.
- C# interceptors can't intercept property gets.
- A materialization interceptor would duplicate what EF's service-property injection already does.

## Repo layout

- `src/Rally25rs.EFCoreLazyLoad/`: the runtime library (net10.0). It also packs the generator dll into the nupkg's `analyzers/dotnet/cs` (the `_PackSourceGenerator` target in its csproj).
- `src/Rally25rs.EFCoreLazyLoad.Generators/`: the incremental source generator (netstandard2.0, Roslyn 4.14). It matches the attribute by metadata name and does not reference the runtime lib. Diagnostics are EFLL001–EFLL010, tracked in `AnalyzerReleases.*.md`.
- `tests/Rally25rs.EFCoreLazyLoad.Tests/`: xUnit v3 integration tests against a real SQLite database. It references the generator as an analyzer (`OutputItemType="Analyzer"`).
  - `Data/`: test entities, `TestDbContext`, and `TestDatabaseFixture`. `Customer` (1-to-1 `Address`, 1-to-many `Hobby`) uses plain `[LazyLoad]` and follows the context options. `Supplier` (1-to-1 `Contact`, 1-to-many `Product`) uses `[LazyLoad(...)]` overrides; it is exercised by `EntityOverrideTests`. `Order` (1-to-many `OrderLine`) is a DDD-style aggregate: a private `_lines` list exposed as a get-only `IEnumerable<OrderLine> Lines`, changed through `AddLine`. All navigations are non-nullable partials.
  - `NavigationSettingsTests`: unit tests (no database) for attribute override resolution. The runtime lib has `InternalsVisibleTo` for this test project.
  - `Support/`: `TestHost` (a fresh DI container and context per test), `SqlCapture` (a command interceptor; `During(action)` returns the SQL run inside it), and `LogCapture`.
- `tests/Rally25rs.EFCoreLazyLoad.Specification.Tests/`: integration tests for use with Ardalis.Specification (Includes/ThenIncludes, missing Includes in Load and Throw mode, no-tracking specs, `Select` projections, per-entity overrides). It is a separate project so Ardalis.Specification stays out of the core test project's dependencies. It compiles the core project's `Data/` and `Support/` files via linked `<Compile Include>` (no project reference to a test project), so both use the same model and seed data. The linked fixture locates *this* project's directory, so it has its own `TestDb/test.db`. Its own `Support/` adds `TestRepository<T>` (a `RepositoryBase<T>`), `CreateSpecificationHost(...)` (also registers the repositories), `host.Repository<T>()` (a repository over `host.Context`), and `Sql.DuringAsync(...)`. Specs live in `Specs/`.
- `tests/Rally25rs.EFCoreLazyLoad.Generators.Tests/`: generator unit tests via `CSharpGeneratorDriver` (output, diagnostics, hierarchy, incremental caching).
- `samples/`: all demo/sample apps go here, never in `src/` or `tests/`. Samples are `IsPackable=false` and reference the library and generator via ProjectReference, the same way the test project does.
  - `samples/Rally25rs.EFCoreLazyLoad.Sample/`: a Generic Host console app (Blog/Author/Posts on SQLite). It demonstrates Include, Load mode with warnings, and Throw mode. Run it with `dotnet run --project samples/Rally25rs.EFCoreLazyLoad.Sample`.
  - `samples/Rally25rs.EFCoreLazyLoad.Specification.Sample/`: the same kind of app using Ardalis.Specification (Customer/Order/OrderLine/Product). It demonstrates a spec with Include/ThenInclude, a spec missing Includes in Load and Throw mode, and a `Select` projection spec, through a `RepositoryBase<T>` repository. Run it with `dotnet run --project samples/Rally25rs.EFCoreLazyLoad.Specification.Sample`.
- `README.md`: consumer-facing documentation only (see below).

## Commands

- Build: `dotnet build` (must be 0 warnings; src projects treat warnings as errors)
- Test: `dotnet test`
- Pack: `dotnet pack src/Rally25rs.EFCoreLazyLoad -o <dir>`

## CI and releasing

- `.github/workflows/ci.yml` runs on PRs to `main` and pushes to `main`: restore, build, test (TRX results uploaded as the `test-results` artifact), and a pack smoke check (the `nupkg` artifact).
- **To release**, publish a GitHub release with a tag `vX.Y.Z` (prereleases like `vX.Y.Z-beta.1` are fine). `.github/workflows/publish.yml` takes the version from the tag (leading `v` stripped; a non-SemVer tag fails the run), runs CI with `-p:Version=<version>`, pushes the package to GitHub Packages (`https://nuget.pkg.github.com/<owner>/index.json`), and attaches the nupkg to the release. The csproj has no version; don't add one.
- `RepositoryUrl` is passed by the workflow (`-p:RepositoryUrl=<server>/<repo>`) so the package links to the repo in GitHub Packages.
- Consumers of the GitHub Packages feed authenticate with a PAT that has `read:packages`, added via `dotnet nuget add source <feed url> --name github --username <user> --password <PAT>` (or a `nuget.config`).

## Conventions

- **README.md is consumer-facing only.** It covers the purpose and usage of the package. Do not put implementation details, technical decisions, or contributor notes in it; those belong in this file or in code comments. Keep it in sync when the public API, options, log events or diagnostics change.
- **Target EF Core 10.x**: the library accepts `[10.0.0, 11.0)` so consumers aren't forced onto a newer patch, and it compiles against 10.0.0, so it can only use APIs present there. Don't raise the lower bound unless a fix requires it. EF Core 11 targets net11 only and will not restore on net10.0. Tests and samples use `$(EFCoreTestVersion)` (`Directory.Build.props`, default the latest pinned 10.0.x) for EF Core and Microsoft.Extensions packages. CI's `test-min-efcore` job reruns the tests with `-p:EFCoreTestVersion=10.0.0`; locally, run `dotnet test -p:EFCoreTestVersion=10.0.0`, then a plain `dotnet restore` to go back. EF Sqlite 10.0.0 reports NU1903 for SQLitePCLRaw in that run; it is test-only and harmless. The runtime lib uses EF internal types (`LazyLoader`, `ILazyLoaderFactory`), so EF1001 is suppressed in that project only. Re-verify these types when moving to a new EF major version.
- **Ardalis.Specification is pinned to 9.3.1** in the sample and its test project only; the library itself doesn't depend on it. 9.x ships net8.0/net9.0 assets that need EF Core >= 9.0.8, so it resolves against our EF Core 10. When a version with a net10.0 asset ships, check that its EF dependency stays below 11.
- **Keep the public surface minimal.** Public types: `LazyLoadAttribute`, `LazyLoadMode`, `LazyLoadOptions`, `NavigationNotLoadedException`, `LazyLoadStackTrace`, the `UseLazyLoad` overloads, and `AddEFCoreLazyLoad`. Everything else is `internal`, under `Rally25rs.EFCoreLazyLoad.Internal`.
- **Tests use a real database, not the in-memory provider.** The library's behavior depends on the actual SQL EF Core generates, so tests assert on captured SQL. `TestDatabaseFixture` is an xUnit v3 **assembly fixture**: it recreates and seeds a SQLite file at `tests/Rally25rs.EFCoreLazyLoad.Tests/TestDb/test.db` (gitignored) once per test run. Tests only read, so test classes run in parallel.
- **Each test creates its own host and context** via `db.CreateHost(...)` / `CreatePooledHost(...)`. Sharing a context would let the change tracker fix up relations and hide whether they were really loaded.
- Test entity navigations are non-nullable `partial` properties on purpose: the library makes the non-nullable declaration truthful. The baseline test shows raw EF behavior (null) by setting `ChangeTracker.LazyLoadingEnabled = false`.
- Pass `TestContext.Current.CancellationToken` to async calls in tests (xUnit1051).
