using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>No-tracking queries, context pooling, and entities that outlive their context.</summary>
public class TrackingAndLifetimeTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AsNoTracking_NavigationIsLazyLoadedOnce()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .AsNoTracking()
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City)));
        Assert.Empty(host.Sql.During(() => _ = customer.Address));
        Assert.Single(host.Sql.During(() => Assert.Equal(3, customer.Hobbies.Count)));
    }

    [Fact]
    public async Task AsNoTrackingWithIdentityResolution_NavigationIsLazyLoaded()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .AsNoTrackingWithIdentityResolution()
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Equal(3, customer.Hobbies.Count)));
    }

    [Fact]
    public async Task AsNoTracking_ThrowMode_Throws()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        var customer = await host.Context.Customers
            .AsNoTracking()
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Empty(host.Sql.During(() => Assert.Throws<NavigationNotLoadedException>(() => customer.Address)));
    }

    [Fact]
    public async Task AddDbContextPool_LazyLoadsAndLogs_InReusedContexts()
    {
        await using var host = db.CreatePooledHost(o => o.LogLazyLoads = true);

        TestDbContext first;
        using (var scope = host.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var customer = await first.Customers
                .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
            Assert.Single(host.Sql.During(() => Assert.Equal(3, customer.Hobbies.Count)));
        }

        using (var scope = host.CreateScope())
        {
            var second = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Same(first, second);
            var customer = await second.Customers
                .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
            Assert.Single(host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City)));
        }

        Assert.Equal(2, host.Logs.LazyLoadEntries.Count);
    }

    [Fact]
    public async Task AddDbContextPool_EntityHeldAfterContextReturnedToPool_ThrowsEfException()
    {
        await using var host = db.CreatePooledHost();

        Customer customer;
        using (var scope = host.CreateScope())
        {
            customer = await scope.ServiceProvider.GetRequiredService<TestDbContext>().Customers
                .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
        }

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => customer.Address);
        Assert.IsNotType<NavigationNotLoadedException>(ex);
    }

    [Theory]
    [InlineData(LazyLoadMode.Load)]
    [InlineData(LazyLoadMode.Throw)]
    public async Task DisposedContext_AccessingUnloadedNavigation_ThrowsEfException(LazyLoadMode mode)
    {
        Customer customer;
        var host = db.CreateHost(o => o.Mode = mode);
        await using (host)
        {
            customer = await host.Context.Customers
                .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
        }

        // EF Core's own LazyLoadOnDisposedContextWarning (an InvalidOperationException), not ours: once the
        // context is gone there is nothing to load from, whatever the mode.
        var ex = Assert.ThrowsAny<InvalidOperationException>(() => customer.Address);
        Assert.IsNotType<NavigationNotLoadedException>(ex);
        Assert.Contains("disposed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisposedContext_NavigationLoadedBeforehand_IsStillReturned()
    {
        Customer customer;
        var host = db.CreateHost();
        await using (host)
        {
            customer = await host.Context.Customers
                .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
            _ = customer.Address;
        }

        Assert.Equal("Springfield", customer.Address.City);
    }
}
