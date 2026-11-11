using Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests;

/// <summary>
///     Specifications with <c>Select</c>. Navigations read inside the projection are translated to SQL by EF Core,
///     so no entity is materialized and nothing is lazy-loaded or blocked.
/// </summary>
public class ProjectionTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(LazyLoadMode.Load)]
    [InlineData(LazyLoadMode.Throw)]
    public async Task DtoProjection_ReadsNavigationsInOneQuery_WithoutMaterializingEntities(LazyLoadMode mode)
    {
        await using var host = db.CreateSpecificationHost(o =>
        {
            o.Mode = mode;
            o.LogLazyLoads = true;
        });
        var repository = host.Repository<Customer>();

        var query = await host.Sql.DuringAsync(async () => await repository.SingleOrDefaultAsync(
            new CustomerSummarySpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct));

        Assert.Equal(new CustomerSummary("Jane Doe", "Springfield", 3), query.Result);
        var sql = Assert.Single(query.Sql);
        Assert.Contains("\"Addresses\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Hobbies\"", sql, StringComparison.Ordinal);
        Assert.Empty(host.Context.ChangeTracker.Entries());
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task ListOfProjections_RunsOneQuery()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var repository = host.Repository<Customer>();

        var query = await host.Sql.DuringAsync(async () => await repository.ListAsync(
            new CustomerSummarySpec(TestDatabaseFixture.CustomerWithoutHobbiesId), Ct));

        Assert.Equal([new CustomerSummary("John Roe", "Shelbyville", 0)], query.Result);
        Assert.Single(query.Sql);
        Assert.Empty(host.Context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task LoadMode_ProjectedEntity_LazyLoadsItsOwnNavigations()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var address = await host.Repository<Customer>().SingleOrDefaultAsync(
            new AddressOfCustomerSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(address);

        var access = host.Sql.During(() => Assert.Equal("Jane Doe", address.Customer.Name));

        Assert.Contains("FROM \"Customers\"", Assert.Single(access), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowMode_ProjectedEntity_ThrowsForItsOwnNavigations()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var address = await host.Repository<Customer>().SingleOrDefaultAsync(
            new AddressOfCustomerSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        Assert.NotNull(address);

        var ex = Assert.Throws<NavigationNotLoadedException>(() => address.Customer);

        Assert.Equal(typeof(Address), ex.EntityType);
        Assert.Equal(nameof(Address.Customer), ex.NavigationName);
    }
}
