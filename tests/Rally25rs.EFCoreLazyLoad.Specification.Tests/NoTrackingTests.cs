using Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests;

/// <summary>Specifications with <c>AsNoTracking</c> or <c>AsNoTrackingWithIdentityResolution</c>.</summary>
public class NoTrackingTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadMode_NoTrackingSpec_LazyLoadsMissingNavigation(bool withIdentityResolution)
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerByIdNoTrackingSpec(
                TestDatabaseFixture.CustomerWithHobbiesId, withIdentityResolution: withIdentityResolution), Ct);
        Assert.NotNull(customer);

        var firstAccess = host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City));
        var secondAccess = host.Sql.During(() => _ = customer.Address);

        Assert.Contains("FROM \"Addresses\"", Assert.Single(firstAccess), StringComparison.Ordinal);
        Assert.Empty(secondAccess);
        Assert.Empty(host.Context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrowMode_NoTrackingSpec_ThrowsForMissingNavigation(bool withIdentityResolution)
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerByIdNoTrackingSpec(
                TestDatabaseFixture.CustomerWithHobbiesId, withIdentityResolution: withIdentityResolution), Ct);
        Assert.NotNull(customer);

        var sql = host.Sql.During(() =>
            Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies));

        Assert.Empty(sql);
    }

    [Fact]
    public async Task ThrowMode_NoTrackingSpecWithInclude_ReturnsIncludedNavigation()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var customer = await host.Repository<Customer>().SingleOrDefaultAsync(
            new CustomerByIdNoTrackingSpec(TestDatabaseFixture.CustomerWithHobbiesId, includeAddress: true), Ct);
        Assert.NotNull(customer);

        Assert.Empty(host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City)));
        Assert.Throws<NavigationNotLoadedException>(() => customer.Hobbies);
    }
}
