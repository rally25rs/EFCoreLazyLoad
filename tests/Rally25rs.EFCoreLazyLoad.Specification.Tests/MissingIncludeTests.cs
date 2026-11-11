using Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests;

/// <summary>
///     A specification that forgets an Include: in <see cref="LazyLoadMode.Load" /> mode the navigation is loaded on
///     first access (one query), in <see cref="LazyLoadMode.Throw" /> mode reading it throws without querying.
/// </summary>
public class MissingIncludeTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoadMode_SpecWithoutIncludes_LazyLoadsEachNavigationOnce()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerByIdSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(customer);

        var addressAccess = host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City));
        var hobbiesAccess = host.Sql.During(() => Assert.Equal(3, customer.Hobbies.Count));
        var secondAccess = host.Sql.During(() => _ = (customer.Address, customer.Hobbies));

        Assert.Contains("FROM \"Addresses\"", Assert.Single(addressAccess), StringComparison.Ordinal);
        Assert.Contains("FROM \"Hobbies\"", Assert.Single(hobbiesAccess), StringComparison.Ordinal);
        Assert.Empty(secondAccess);
    }

    [Fact]
    public async Task LoadMode_SpecMissingOneInclude_LazyLoadsOnlyThatNavigation_AndLogsIt()
    {
        await using var host = db.CreateSpecificationHost(o =>
        {
            o.Mode = LazyLoadMode.Load;
            o.LogLazyLoads = true;
        });
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerWithAddressSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(customer);

        Assert.Empty(host.Sql.During(() => _ = customer.Address));
        Assert.Single(host.Sql.During(() => _ = customer.Hobbies));

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal("LazyLoad", entry.EventId.Name);
        Assert.Contains("Customer.Hobbies", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadMode_SpecMissingThenInclude_LazyLoadsTheNestedNavigation()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var hobbies = await host.Repository<Hobby>().ListAsync(
            new HobbiesWithCustomerSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);

        Assert.Empty(host.Sql.During(() => _ = hobbies[0].Customer));
        var firstAccess = host.Sql.During(() => Assert.Equal("Springfield", hobbies[0].Customer.Address.City));
        // Every hobby shares the one tracked customer, so its address is loaded only once.
        var otherAccess = host.Sql.During(() => Assert.All(hobbies, h => _ = h.Customer.Address));

        Assert.Contains("FROM \"Addresses\"", Assert.Single(firstAccess), StringComparison.Ordinal);
        Assert.Empty(otherAccess);
    }

    [Fact]
    public async Task LoadMode_ListSpecWithoutInclude_RunsOneQueryPerEntity()
    {
        await using var host = db.CreateSpecificationHost(o =>
        {
            o.Mode = LazyLoadMode.Load;
            o.LogLazyLoads = true;
        });
        var customers = await host.Repository<Customer>().ListAsync(new CustomersSpec(), Ct);

        var access = host.Sql.During(() => Assert.All(customers, c => Assert.NotNull(c.Address)));

        // The N+1 pattern: one lazy load (and one log entry) per customer.
        Assert.Equal(customers.Count, access.Count);
        Assert.Equal(customers.Count, host.Logs.LazyLoadEntries.Count);
    }

    [Fact]
    public async Task ThrowMode_SpecWithoutIncludes_ThrowsWithoutQuerying()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerByIdSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(customer);

        var sql = host.Sql.During(() =>
        {
            var address = Assert.Throws<NavigationNotLoadedException>(() => customer.Address);
            Assert.Equal(typeof(Customer), address.EntityType);
            Assert.Equal(nameof(Customer.Address), address.NavigationName);

            var hobbies = Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies);
            Assert.Equal(nameof(Customer.Hobbies), hobbies.NavigationName);
        });

        Assert.Empty(sql);
    }

    [Fact]
    public async Task ThrowMode_SpecMissingOneInclude_ThrowsOnlyForThatNavigation_AndLogsIt()
    {
        await using var host = db.CreateSpecificationHost(o =>
        {
            o.Mode = LazyLoadMode.Throw;
            o.LogLazyLoads = true;
        });
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerWithAddressSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(customer);

        Assert.Equal("Springfield", customer.Address.City);
        var ex = Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies);

        Assert.Equal(nameof(Customer.Hobbies), ex.NavigationName);
        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal("LazyLoadBlocked", entry.EventId.Name);
        Assert.Contains("Customer.Hobbies", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowMode_SpecMissingThenInclude_ThrowsForTheNestedNavigation()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var hobbies = await host.Repository<Hobby>().ListAsync(
            new HobbiesWithCustomerSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);

        var customer = hobbies[0].Customer;
        var ex = Assert.Throws<NavigationNotLoadedException>(() => customer.Address);

        Assert.Equal(typeof(Customer), ex.EntityType);
        Assert.Equal(nameof(Customer.Address), ex.NavigationName);
    }
}
