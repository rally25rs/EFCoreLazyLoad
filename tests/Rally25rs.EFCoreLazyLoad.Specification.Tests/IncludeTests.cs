using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests;

/// <summary>
///     Navigations a specification includes are loaded by its query and returned as-is: reading them runs no SQL,
///     and doesn't throw even in <see cref="LazyLoadMode.Throw" /> mode.
/// </summary>
public class IncludeTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Includes_PopulateNavigations_WithNoExtraSql()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var repository = host.Repository<Customer>();

        var query = await host.Sql.DuringAsync(async () => await repository.SingleOrDefaultAsync(
            new CustomerWithDetailsSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct));
        var customer = Assert.IsType<Customer>(query.Result);

        var access = host.Sql.During(() =>
        {
            Assert.Equal("Springfield", customer.Address.City);
            Assert.Equal(["Chess", "Cycling", "Photography"], customer.Hobbies.Select(h => h.Name).Order());
        });

        var sql = Assert.Single(query.Sql);
        Assert.Contains("\"Addresses\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Hobbies\"", sql, StringComparison.Ordinal);
        Assert.Empty(access);
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task IncludeThenInclude_PopulatesReferenceChain_WithNoExtraSql()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var hobbies = await host.Repository<Hobby>().ListAsync(
            new HobbiesWithCustomerAndAddressSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);

        var access = host.Sql.During(() =>
        {
            Assert.Equal(3, hobbies.Count);
            Assert.All(hobbies, h => Assert.Equal("Springfield", h.Customer.Address.City));
        });

        Assert.Empty(access);
    }

    [Fact]
    public async Task IncludeThenInclude_PopulatesCollection_WithNoExtraSql()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var address = await host.Repository<Address>().SingleOrDefaultAsync(
            new AddressWithCustomerAndHobbiesSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);

        Assert.NotNull(address);
        var access = host.Sql.During(() => Assert.Equal(3, address.Customer.Hobbies.Count));

        Assert.Empty(access);
    }

    [Fact]
    public async Task SplitQueryIncludes_PopulateNavigations_WithNoExtraSql()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var repository = host.Repository<Customer>();

        var query = await host.Sql.DuringAsync(async () => await repository.SingleOrDefaultAsync(
            new CustomerWithDetailsSplitSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct));
        var customer = Assert.IsType<Customer>(query.Result);

        var access = host.Sql.During(() =>
        {
            Assert.Equal("Springfield", customer.Address.City);
            Assert.Equal(3, customer.Hobbies.Count);
        });

        Assert.Equal(2, query.Sql.Count);
        Assert.Empty(access);
    }

    [Fact]
    public async Task WithSpecification_OnDbSet_PopulatesIncludedNavigations()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var customer = await host.Context.Customers
            .WithSpecification(new CustomerWithDetailsSpec(TestDatabaseFixture.CustomerWithHobbiesId))
            .SingleAsync(Ct);

        Assert.Empty(host.Sql.During(() => _ = (customer.Address, customer.Hobbies)));
    }

    [Fact]
    public async Task RepositoriesResolvedFromDependencyInjection_PopulateIncludedNavigations()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        using var scope = host.CreateScope();
        var readRepository = scope.ServiceProvider.GetRequiredService<IReadRepositoryBase<Customer>>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositoryBase<Customer>>();

        var viaRead = await readRepository.SingleOrDefaultAsync(
            new CustomerWithDetailsSpec(TestDatabaseFixture.CustomerWithHobbiesId), Ct);
        var viaWrite = await repository.SingleOrDefaultAsync(
            new CustomerWithDetailsSpec(TestDatabaseFixture.CustomerWithoutHobbiesId), Ct);

        Assert.NotNull(viaRead);
        Assert.NotNull(viaWrite);
        Assert.Empty(host.Sql.During(() =>
        {
            Assert.Equal("Springfield", viaRead.Address.City);
            Assert.Equal(3, viaRead.Hobbies.Count);
            Assert.Equal("Shelbyville", viaWrite.Address.City);
            Assert.Empty(viaWrite.Hobbies);
        }));
    }
}
