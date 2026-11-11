using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>
///     DDD-style collections: a private <c>List&lt;T&gt;</c> backing field exposed as a read-only
///     <c>IEnumerable&lt;T&gt;</c> and changed only through a domain method (<see cref="Order" />).
/// </summary>
public class ReadOnlyCollectionTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Include_PopulatesBackingField_WithoutLazyLoading()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .Include(o => o.Lines)
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        var access = host.Sql.During(() => Assert.Equal(2, order.Lines.Count()));

        Assert.Empty(access);
        Assert.Equal(["Gadget", "Widget"], order.Lines.Select(l => l.ProductName).Order());
    }

    [Fact]
    public async Task WithoutInclude_IsLazyLoadedOnceOnFirstAccess()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        var firstAccess = host.Sql.During(() => Assert.Equal(2, order.Lines.Count()));
        var secondAccess = host.Sql.During(() => _ = order.Lines.Count());

        var sql = Assert.Single(firstAccess);
        Assert.Contains("FROM \"OrderLines\"", sql, StringComparison.Ordinal);
        Assert.Empty(secondAccess);
        Assert.All(order.Lines, l => Assert.Same(order, l.Order));
    }

    [Fact]
    public async Task EmptyCollection_IsLazyLoadedAsEmpty()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithoutLinesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Empty(order.Lines)));
        Assert.Empty(host.Sql.During(() => Assert.Empty(order.Lines)));
    }

    [Fact]
    public async Task ExposedCollection_IsReadOnly()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        // The getter returns `_lines.AsReadOnly()`, so callers can't cast back to the list and modify it.
        Assert.IsNotType<List<OrderLine>>(order.Lines);
        var asCollection = Assert.IsAssignableFrom<ICollection<OrderLine>>(order.Lines);
        Assert.True(asCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asCollection.Add(new OrderLine { ProductName = "Sneaky" }));
    }

    [Fact]
    public async Task DomainMethod_ReadingTheProperty_LazyLoadsBeforeCheckingInvariants()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        // AddLine checks for a duplicate through `Lines`, which loads the existing lines first, so the duplicate
        // is caught even though the query didn't include them.
        var sql = host.Sql.During(() =>
            Assert.Throws<InvalidOperationException>(() => order.AddLine("Widget", 5)));
        Assert.Single(sql);

        order.AddLine("Sprocket", 3);
        Assert.Equal(["Gadget", "Sprocket", "Widget"], order.Lines.Select(l => l.ProductName).Order());
    }

    [Fact]
    public async Task AsNoTracking_IsLazyLoaded()
    {
        await using var host = db.CreateHost();
        var order = await host.Context.Orders
            .AsNoTracking()
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Equal(2, order.Lines.Count())));
    }

    [Fact]
    public async Task ThrowMode_Throws_WithoutQuerying()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        NavigationNotLoadedException? ex = null;
        var sql = host.Sql.During(() => ex = Assert.Throws<NavigationNotLoadedException>(() => order.Lines));

        Assert.Empty(sql);
        Assert.Equal(typeof(Order), ex!.EntityType);
        Assert.Equal(nameof(Order.Lines), ex.NavigationName);
        Assert.Contains(".Include(e => e.Lines)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowMode_DomainMethod_Throws_InsteadOfCheckingAnUnloadedList()
    {
        await using var host = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => order.AddLine("Widget", 5));
    }

    [Fact]
    public async Task LazyLoad_IsLogged()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        _ = order.Lines.Count();

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Contains("'Order.Lines'", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithLazyLoadingDisabled_ExposesTheUnloadedEmptyList()
    {
        // Raw EF behavior: the pre-initialized backing field stays empty (not null) when nothing loads it.
        await using var host = db.CreateHost();
        host.Context.ChangeTracker.LazyLoadingEnabled = false;
        var order = await host.Context.Orders
            .SingleAsync(o => o.Id == TestDatabaseFixture.OrderWithLinesId, Ct);

        Assert.Empty(host.Sql.During(() => Assert.Empty(order.Lines)));
    }
}
