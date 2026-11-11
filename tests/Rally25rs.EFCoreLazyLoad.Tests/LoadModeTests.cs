using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary><see cref="LazyLoadMode.Load" />: unloaded navigations are loaded on first access.</summary>
public class LoadModeTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReferenceNavigation_WithoutInclude_IsLazyLoadedOnceOnFirstAccess()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Address? address = null;
        var firstAccess = host.Sql.During(() => address = customer.Address);
        var secondAccess = host.Sql.During(() => _ = customer.Address);

        Assert.NotNull(address);
        Assert.Equal("Springfield", address.City);
        var sql = Assert.Single(firstAccess);
        Assert.Contains("FROM \"Addresses\"", sql, StringComparison.Ordinal);
        Assert.Empty(secondAccess);
        Assert.Same(address, customer.Address);
    }

    [Fact]
    public async Task CollectionNavigation_WithoutInclude_IsLazyLoadedOnceOnFirstAccess()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        var firstAccess = host.Sql.During(() => Assert.Equal(3, customer.Hobbies.Count));
        var secondAccess = host.Sql.During(() => _ = customer.Hobbies.Count);

        var sql = Assert.Single(firstAccess);
        Assert.Contains("FROM \"Hobbies\"", sql, StringComparison.Ordinal);
        Assert.Empty(secondAccess);
        Assert.Equal(["Chess", "Cycling", "Photography"], customer.Hobbies.Select(h => h.Name).Order());
    }

    [Fact]
    public async Task EmptyCollectionNavigation_IsLazyLoadedAsEmptyList()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithoutHobbiesId, Ct);

        List<Hobby>? hobbies = null;
        var firstAccess = host.Sql.During(() => hobbies = customer.Hobbies);
        var secondAccess = host.Sql.During(() => _ = customer.Hobbies);

        Assert.NotNull(hobbies);
        Assert.Empty(hobbies);
        Assert.Single(firstAccess);
        Assert.Empty(secondAccess);
    }

    [Fact]
    public async Task BackReferenceNavigation_WithoutInclude_IsLazyLoaded()
    {
        await using var host = db.CreateHost();
        var hobby = await host.Context.Hobbies
            .FirstAsync(h => h.CustomerId == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Customer? customer = null;
        var firstAccess = host.Sql.During(() => customer = hobby.Customer);

        Assert.NotNull(customer);
        Assert.Equal("Jane Doe", customer.Name);
        var sql = Assert.Single(firstAccess);
        Assert.Contains("FROM \"Customers\"", sql, StringComparison.Ordinal);
        Assert.Empty(host.Sql.During(() => _ = hobby.Customer));
    }

    [Fact]
    public async Task OneToOneBackReference_WithoutInclude_IsLazyLoaded()
    {
        await using var host = db.CreateHost();
        var address = await host.Context.Addresses
            .SingleAsync(a => a.CustomerId == TestDatabaseFixture.CustomerWithoutHobbiesId, Ct);

        var firstAccess = host.Sql.During(() => Assert.Equal("John Roe", address.Customer.Name));

        Assert.Single(firstAccess);
    }

    [Fact]
    public async Task IncludedNavigation_IsNotLoadedAgain_WhileOthersStillLazyLoad()
    {
        await using var host = db.CreateHost();
        var customer = await host.Context.Customers
            .Include(c => c.Address)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Empty(host.Sql.During(() => _ = customer.Address));
        Assert.Single(host.Sql.During(() => _ = customer.Hobbies));
    }

    [Fact]
    public async Task DefaultOptions_UseLoadMode()
    {
        await using var host = db.CreateHost(configure: null);
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.NotNull(customer.Address)));
    }
}
