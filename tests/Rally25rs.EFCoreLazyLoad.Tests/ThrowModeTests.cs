using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary><see cref="LazyLoadMode.Throw" />: accessing an unloaded navigation throws instead of querying.</summary>
public class ThrowModeTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private Support.TestHost CreateHost()
        => db.CreateHost(o => o.Mode = LazyLoadMode.Throw);

    [Fact]
    public async Task UnloadedReferenceNavigation_Throws_WithoutQuerying()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        NavigationNotLoadedException? ex = null;
        var sql = host.Sql.During(() => ex = Assert.Throws<NavigationNotLoadedException>(() => customer.Address));

        Assert.Empty(sql);
        Assert.Equal(typeof(Customer), ex!.EntityType);
        Assert.Equal(nameof(Customer.Address), ex.NavigationName);
        Assert.Contains("Customer.Address", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".Include(e => e.Address)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnloadedCollectionNavigation_Throws_WithoutQuerying()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        NavigationNotLoadedException? ex = null;
        var sql = host.Sql.During(() => ex = Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies));

        Assert.Empty(sql);
        Assert.Equal(nameof(Customer.Hobbies), ex!.NavigationName);
    }

    [Fact]
    public async Task UnloadedNavigation_ThrowsOnEveryAccess()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => customer.Address);
        Assert.Throws<NavigationNotLoadedException>(() => customer.Address);
        Assert.Equal(1, host.Sql.Count);
    }

    [Fact]
    public async Task IncludedNavigations_AreReturned()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .Include(c => c.Address)
            .Include(c => c.Hobbies)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        var sql = host.Sql.During(() =>
        {
            Assert.Equal("Springfield", customer.Address.City);
            Assert.Equal(3, customer.Hobbies.Count);
        });

        Assert.Empty(sql);
    }

    [Fact]
    public async Task IncludedEmptyCollection_IsReturned()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .Include(c => c.Hobbies)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithoutHobbiesId, Ct);

        Assert.Empty(customer.Hobbies);
    }

    [Fact]
    public async Task IncludedNavigation_NoTracking_IsReturned()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .AsNoTracking()
            .Include(c => c.Address)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Equal("Springfield", customer.Address.City);
        Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies);
    }

    [Fact]
    public async Task NavigationLoadedExplicitly_IsReturned()
    {
        await using var host = CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        await host.Context.Entry(customer).Reference(c => c.Address).LoadAsync(Ct);

        Assert.Equal("Springfield", customer.Address.City);
    }

    [Fact]
    public async Task LazyLoadingDisabledOnChangeTracker_LeavesNavigationNull_WithoutThrowing()
    {
        await using var host = CreateHost();
        host.Context.ChangeTracker.LazyLoadingEnabled = false;
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Null(customer.Address);
        Assert.Null(customer.Hobbies);
    }

    [Fact]
    public async Task BackReferenceNavigation_Throws()
    {
        await using var host = CreateHost();
        var hobby = await host.Context.Hobbies
            .FirstAsync(h => h.CustomerId == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        var ex = Assert.Throws<NavigationNotLoadedException>(() => hobby.Customer);

        Assert.Equal(typeof(Hobby), ex.EntityType);
        Assert.Equal(nameof(Hobby.Customer), ex.NavigationName);
    }
}
