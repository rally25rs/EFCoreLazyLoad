using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>
///     <see cref="LazyLoadAttribute" /> settings override the context's <see cref="LazyLoadOptions" /> for an entity or
///     a single navigation. See <see cref="Supplier" />, <see cref="Contact" /> and <see cref="Product" /> for the setup.
/// </summary>
public class EntityOverrideTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClassLevelThrow_OverridesContextLoadMode()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        var sql = host.Sql.During(() =>
        {
            var ex = Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);
            Assert.Equal(typeof(Supplier), ex.EntityType);
            Assert.Equal(nameof(Supplier.Contact), ex.NavigationName);
        });

        Assert.Empty(sql);
    }

    [Fact]
    public async Task ClassLevelThrow_IncludedNavigation_IsReturned()
    {
        await using var host = db.CreateHost();
        var supplier = await host.Context.Suppliers
            .Include(s => s.Contact)
            .SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        Assert.Equal("sales@acme.example", supplier.Contact.Email);
    }

    [Fact]
    public async Task PropertyLevelLoad_OverridesClassLevelThrow()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        var sql = host.Sql.During(() => Assert.Equal(2, supplier.Products.Count));

        Assert.Contains("FROM \"Products\"", Assert.Single(sql), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropertyLevelLoad_OverridesContextThrowMode()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        Assert.Equal(2, supplier.Products.Count);
        Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);
    }

    [Fact]
    public async Task UnsetMode_FollowsContextMode()
    {
        // Product sets only logging, so its Mode comes from the context.
        await using (var loadHost = db.CreateHost(o => o.Mode = LazyLoadMode.Load))
        {
            var product = await loadHost.Context.Products.FirstAsync(Ct);
            Assert.Equal("Acme", product.Supplier.Name);
        }

        await using (var throwHost = db.CreateHost(o => o.Mode = LazyLoadMode.Throw))
        {
            var product = await throwHost.Context.Products.FirstAsync(Ct);
            Assert.Throws<NavigationNotLoadedException>(() => product.Supplier);
        }
    }

    [Fact]
    public async Task ClassLevelLogging_LogsAtItsLevel_WhenContextLoggingIsOff()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = false);
        var product = await host.Context.Products.FirstAsync(Ct);

        _ = product.Supplier;

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("LazyLoad", entry.EventId.Name);
        Assert.Contains("Product.Supplier", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassLevelLogging_DoesNotApplyToOtherEntities()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = false);
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Address;

        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task PropertyLevelLoggingOff_OverridesContextLoggingOn()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var contact = await host.Context.Contacts.FirstAsync(Ct);

        Assert.Equal("Acme", contact.Supplier.Name);

        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task UnsetLogLevel_FollowsContextLogLevel()
    {
        // Supplier sets Mode only, so a blocked load logs with the context's logging settings.
        await using var host = db.CreateHost(o =>
        {
            o.LogLazyLoads = true;
            o.LogLevel = LogLevel.Error;
        });
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("LazyLoadBlocked", entry.EventId.Name);
    }

    [Fact]
    public async Task Overrides_ApplyToNoTrackingQueries()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Context.Suppliers
            .AsNoTracking()
            .SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);
        Assert.Equal(2, supplier.Products.Count);
    }

    [Fact]
    public async Task Overrides_ApplyToPooledContexts()
    {
        await using var host = db.CreatePooledHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);
    }

    [Fact]
    public async Task LazyLoadingDisabledOnChangeTracker_DisablesOverrides()
    {
        await using var host = db.CreateHost();
        host.Context.ChangeTracker.LazyLoadingEnabled = false;
        var supplier = await host.Context.Suppliers.SingleAsync(s => s.Id == TestDatabaseFixture.SupplierId, Ct);

        // Raw EF behavior: the class-level Throw is not applied, the navigation is just not loaded.
        Assert.Null(supplier.Contact);
    }
}
