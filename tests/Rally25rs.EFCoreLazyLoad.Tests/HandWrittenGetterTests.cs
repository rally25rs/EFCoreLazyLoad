using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>
///     <see cref="Shipment" />: a hand-written getter that asks the entity's own <c>ILazyLoader</c> member to load
///     (for collections the generator can't implement, such as one copied under a lock), and a generated get-only
///     partial over a nullable <c>List&lt;T&gt;?</c> backing field.
/// </summary>
public class HandWrittenGetterTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public void UserDeclaredLoaderMember_IsUsedInsteadOfAGeneratedOne()
        => Assert.Null(typeof(Shipment).GetField("__lazyLoader", BindingFlags.Instance | BindingFlags.NonPublic));

    [Fact]
    public async Task HandWrittenGetter_WithoutInclude_IsLazyLoadedOnce()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.ShipmentWithParcelsId, Ct);

        var firstAccess = host.Sql.During(() => Assert.Equal(2, shipment.Parcels.Count));
        var secondAccess = host.Sql.During(() => _ = shipment.Parcels.Count);

        Assert.Contains("FROM \"Parcels\"", Assert.Single(firstAccess), StringComparison.Ordinal);
        Assert.Empty(secondAccess);
        Assert.All(shipment.Parcels, p => Assert.Same(shipment, p.Shipment));
    }

    [Fact]
    public async Task HandWrittenGetter_WithInclude_DoesNotQuery()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .Include(s => s.Parcels)
            .SingleAsync(s => s.Id == TestDatabaseFixture.ShipmentWithParcelsId, Ct);

        Assert.Empty(host.Sql.During(() => Assert.Equal(2, shipment.Parcels.Count)));
    }

    [Fact]
    public async Task HandWrittenGetter_EmptyCollection_IsLazyLoadedAsEmpty()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.EmptyShipmentId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Empty(shipment.Parcels)));
    }

    [Fact]
    public async Task HandWrittenGetter_InThrowMode_Throws()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.ShipmentWithParcelsId, Ct);

        var ex = Assert.Throws<NavigationNotLoadedException>(() => shipment.Parcels);

        Assert.Equal("Parcels", ex.NavigationName);
    }

    [Fact]
    public async Task HandWrittenGetter_IsLogged()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.ShipmentWithParcelsId, Ct);

        _ = shipment.Parcels;

        Assert.Contains("Shipment.Parcels", Assert.Single(host.Logs.LazyLoadEntries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullableBackingField_WithoutInclude_IsLazyLoaded()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.ShipmentWithParcelsId, Ct);

        var access = host.Sql.During(() => Assert.Equal(["Fragile"], shipment.Labels.Select(l => l.Text)));

        Assert.Contains("FROM \"Labels\"", Assert.Single(access), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullableBackingField_EmptyCollection_IsLazyLoadedAsEmpty_NotNull()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .SingleAsync(s => s.Id == TestDatabaseFixture.EmptyShipmentId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Empty(shipment.Labels)));
        Assert.Empty(host.Sql.During(() => Assert.Empty(shipment.Labels)));
    }

    [Fact]
    public async Task NullableBackingField_WithInclude_EmptyCollection_IsEmpty_NotNull()
    {
        await using var host = db.CreateHost();
        var shipment = await host.Context.Shipments
            .Include(s => s.Labels)
            .SingleAsync(s => s.Id == TestDatabaseFixture.EmptyShipmentId, Ct);

        Assert.Empty(host.Sql.During(() => Assert.Empty(shipment.Labels)));
    }
}
