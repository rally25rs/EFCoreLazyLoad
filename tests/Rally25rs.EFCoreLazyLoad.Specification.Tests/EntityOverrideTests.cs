using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;
using Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests;

/// <summary>
///     <see cref="LazyLoadAttribute" /> settings on an entity or navigation still override the context's options when
///     the entity is queried through a specification. See <see cref="Supplier" />, <see cref="Contact" /> and
///     <see cref="Product" /> for the setup.
/// </summary>
public class EntityOverrideTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClassLevelThrow_OverridesContextLoadMode(bool noTracking)
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Repository<Supplier>().SingleOrDefaultAsync(
            new SupplierByIdSpec(TestDatabaseFixture.SupplierId, noTracking), Ct);
        Assert.NotNull(supplier);

        var sql = host.Sql.During(() =>
        {
            var ex = Assert.Throws<NavigationNotLoadedException>(() => supplier.Contact);
            Assert.Equal(typeof(Supplier), ex.EntityType);
            Assert.Equal(nameof(Supplier.Contact), ex.NavigationName);
        });

        Assert.Empty(sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropertyLevelLoad_OverridesClassLevelAndContextThrow(bool noTracking)
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Throw);
        var supplier = await host.Repository<Supplier>().SingleOrDefaultAsync(
            new SupplierByIdSpec(TestDatabaseFixture.SupplierId, noTracking), Ct);
        Assert.NotNull(supplier);

        var sql = host.Sql.During(() => Assert.Equal(2, supplier.Products.Count));

        Assert.Contains("FROM \"Products\"", Assert.Single(sql), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassLevelThrow_SpecWithInclude_ReturnsNavigation()
    {
        await using var host = db.CreateSpecificationHost(o => o.Mode = LazyLoadMode.Load);
        var supplier = await host.Repository<Supplier>().SingleOrDefaultAsync(
            new SupplierWithContactSpec(TestDatabaseFixture.SupplierId), Ct);
        Assert.NotNull(supplier);

        Assert.Empty(host.Sql.During(() => Assert.Equal("sales@acme.example", supplier.Contact.Email)));
    }

    [Fact]
    public async Task ClassLevelLogging_LogsAtItsLevel_WhenContextLoggingIsOff()
    {
        await using var host = db.CreateSpecificationHost(o => o.LogLazyLoads = false);
        var products = await host.Repository<Product>().ListAsync(new ProductsSpec(), Ct);

        Assert.Equal("Acme", products[0].Supplier.Name);

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Product.Supplier", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropertyLevelLoggingOff_OverridesContextLoggingOn()
    {
        await using var host = db.CreateSpecificationHost(o => o.LogLazyLoads = true);
        var contacts = await host.Repository<Contact>().ListAsync(new ContactsSpec(), Ct);

        Assert.Equal("Acme", Assert.Single(contacts).Supplier.Name);

        Assert.Empty(host.Logs.LazyLoadEntries);
    }
}
