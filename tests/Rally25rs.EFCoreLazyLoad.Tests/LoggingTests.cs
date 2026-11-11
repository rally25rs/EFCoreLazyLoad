using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Tests.Data;
using Rally25rs.EFCoreLazyLoad.Tests.Support;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary><see cref="LazyLoadOptions.LogLazyLoads" /> and where its entries are written.</summary>
public class LoggingTests(TestDatabaseFixture db)
{
    private const int LazyLoadEventId = 1;
    private const int LazyLoadBlockedEventId = 2;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogLazyLoads_LogsWarningWithEntityAndNavigation_OncePerLoad()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Address;
        _ = customer.Address;

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(LazyLoadEventId, entry.EventId.Id);
        Assert.Equal("LazyLoad", entry.EventId.Name);
        Assert.Contains("Customer.Address", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogLazyLoads_LogsEachNavigationLoaded()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var hobby = await host.Context.Hobbies
            .FirstAsync(h => h.CustomerId == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = hobby.Customer.Address;

        Assert.Collection(
            host.Logs.LazyLoadEntries,
            e => Assert.Contains("Hobby.Customer", e.Message, StringComparison.Ordinal),
            e => Assert.Contains("Customer.Address", e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogLazyLoads_UsesConfiguredLogLevel()
    {
        await using var host = db.CreateHost(o =>
        {
            o.LogLazyLoads = true;
            o.LogLevel = LogLevel.Information;
        });
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Hobbies;

        Assert.Equal(LogLevel.Information, Assert.Single(host.Logs.LazyLoadEntries).Level);
    }

    [Fact]
    public async Task LogLazyLoads_IncludedNavigation_IsNotLogged()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await host.Context.Customers
            .Include(c => c.Address)
            .Include(c => c.Hobbies)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Address;
        _ = customer.Hobbies;

        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task LogLazyLoadsDisabled_IsNotLogged()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = false);
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => _ = customer.Address));
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task LogLazyLoads_ThrowMode_LogsBlockedLoad()
    {
        await using var host = db.CreateHost(o =>
        {
            o.Mode = LazyLoadMode.Throw;
            o.LogLazyLoads = true;
        });
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => customer.Address);

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(LazyLoadBlockedEventId, entry.EventId.Id);
        Assert.Equal("LazyLoadBlocked", entry.EventId.Name);
        Assert.Contains("Customer.Address", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitLoggerFactory_IsUsedInsteadOfApplicationLoggerFactory()
    {
        var explicitLogs = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(explicitLogs).SetMinimumLevel(LogLevel.Trace));
        await using var host = db.CreateHost(o =>
        {
            o.LogLazyLoads = true;
            o.LoggerFactory = loggerFactory;
        });
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Address;

        Assert.Contains("Customer.Address", Assert.Single(explicitLogs.LazyLoadEntries).Message, StringComparison.Ordinal);
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task ExplicitLoggerFactory_WithoutDependencyInjection_IsUsed()
    {
        var logs = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(db.ConnectionString)
            .UseLazyLoad(o =>
            {
                o.Mode = LazyLoadMode.Throw;
                o.LogLazyLoads = true;
                o.LoggerFactory = loggerFactory;
            })
            .Options;
        await using var context = new TestDbContext(options);
        var customer = await context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies);

        Assert.Equal(LazyLoadBlockedEventId, Assert.Single(logs.LazyLoadEntries).EventId.Id);
    }
}
