# Rally25rs.EFCoreLazyLoad

## Purpose

This package extends EF Core to add the ability to **optionally lazy-load entity relations**. It can also be used to **detect and log, or throw an error, when a relation needed to be lazy-loaded** (i.e. it was accessed without having been loaded up front).

Namespace: `Rally25rs.EFCoreLazyLoad`

## Repo layout

- `src/Rally25rs.EFCoreLazyLoad/` — the class library (net10.0, depends on `Microsoft.EntityFrameworkCore`).
- `tests/Rally25rs.EFCoreLazyLoad.Tests/` — xUnit v3 integration tests against a real SQLite database.
  - `Data/` — test entities (`Customer` 1-to-1 `Address`, `Customer` 1-to-many `Hobby`), `TestDbContext`, and `TestDatabaseFixture`.
- `README.md` — consumer-facing documentation only (see below).

## Commands

- Build: `dotnet build`
- Test: `dotnet test`

## Conventions

- **README.md is consumer-facing only.** It covers the purpose and usage of the package. Do not put implementation details, technical decisions, or contributor notes in it; those belong in this file or in code comments.
- **Target EF Core 10.x** (currently pinned to 10.0.15). EF Core 11 targets net11 only and will not restore on net10.0.
- **Tests use a real database, not the in-memory provider.** The library's behavior depends on the actual SQL EF Core generates, so tests must be able to verify real queries. `TestDatabaseFixture` recreates and seeds a SQLite file at `tests/Rally25rs.EFCoreLazyLoad.Tests/TestDb/test.db` (gitignored) once per test class.
- **Each test creates its own `DbContext`** via `TestDatabaseFixture.CreateContext()`. Sharing a context would let the change tracker fix up relations and hide whether they were really loaded.
- Navigation properties on test entities are nullable and uninitialized on purpose: they must be `null` unless loaded.
- Pass `TestContext.Current.CancellationToken` to async calls in tests (xUnit1051).
