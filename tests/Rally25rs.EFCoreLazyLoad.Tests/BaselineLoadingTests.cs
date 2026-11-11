using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>Eager loading, and the raw EF Core behavior when lazy loading is switched off.</summary>
public class BaselineLoadingTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Include_PopulatesNavigations_WithoutFurtherQueries()
    {
        await using var host = db.CreateHost();

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
    public async Task WithoutInclude_LazyLoadingDisabled_LeavesNavigationsNull()
    {
        await using var host = db.CreateHost();
        host.Context.ChangeTracker.LazyLoadingEnabled = false;

        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        // The navigations are declared non-nullable, yet they are null here: this is plain EF Core behavior when
        // nothing loads them, and exactly the silent "forgot the Include" bug this library exists to guard against.
        var sql = host.Sql.During(() =>
        {
            Assert.Null(customer.Address);
            Assert.Null(customer.Hobbies);
        });

        Assert.Empty(sql);
    }
}
