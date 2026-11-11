using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>The ways of enabling and configuring the library.</summary>
public class ConfigurationTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddEFCoreLazyLoad_WithUseLazyLoadFromServiceProvider_AppliesRegisteredOptions()
    {
        await using var host = db.CreateHost((services, useDatabase) =>
        {
            services.AddEFCoreLazyLoad(o => o.Mode = LazyLoadMode.Throw);
            services.AddDbContext<TestDbContext>((sp, o) =>
            {
                useDatabase(o);
                o.UseLazyLoad(sp);
            });
        });
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Empty(host.Sql.During(() => Assert.Throws<NavigationNotLoadedException>(() => customer.Address)));
    }

    [Fact]
    public async Task AddEFCoreLazyLoad_CalledTwice_AppliesBothConfigurations()
    {
        await using var host = db.CreateHost((services, useDatabase) =>
        {
            services.AddEFCoreLazyLoad(o => o.LogLazyLoads = true);
            services.AddEFCoreLazyLoad(o => o.LogLevel = LogLevel.Error);
            services.AddDbContext<TestDbContext>((sp, o) =>
            {
                useDatabase(o);
                o.UseLazyLoad(sp);
            });
        });
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => _ = customer.Address));

        Assert.Equal(LogLevel.Error, Assert.Single(host.Logs.LazyLoadEntries).Level);
    }

    [Fact]
    public async Task UseLazyLoadFromServiceProvider_WithoutRegisteredOptions_UsesDefaults()
    {
        await using var host = db.CreateHost((services, useDatabase) =>
            services.AddDbContext<TestDbContext>((sp, o) =>
            {
                useDatabase(o);
                o.UseLazyLoad(sp);
            }));
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.NotNull(customer.Address)));
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task UseLazyLoad_CalledTwice_UpdatesPreviousOptions()
    {
        await using var host = db.CreateHost((services, useDatabase) =>
            services.AddDbContext<TestDbContext>(o =>
            {
                o.UseLazyLoad(x => x.LogLazyLoads = true);
                useDatabase(o);
                o.UseLazyLoad(x => x.LogLevel = LogLevel.Information);
            }));
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        _ = customer.Address;

        Assert.Equal(LogLevel.Information, Assert.Single(host.Logs.LazyLoadEntries).Level);
    }

    [Fact]
    public async Task WithoutUseLazyLoad_MarkedNavigations_GetStandardEfLazyLoading()
    {
        await using var host = db.CreateHost((services, useDatabase) =>
            services.AddDbContext<TestDbContext>(useDatabase));
        var customer = await host.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Single(host.Sql.During(() => Assert.Equal("Springfield", customer.Address.City)));
        Assert.Empty(host.Logs.LazyLoadEntries);
    }

    [Fact]
    public async Task OptionsArePerContext_InTheSameProcess()
    {
        await using var throwing = db.CreateHost(o => o.Mode = LazyLoadMode.Throw);
        await using var loading = db.CreateHost(o => o.Mode = LazyLoadMode.Load);

        var a = await throwing.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);
        var b = await loading.Context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        Assert.Throws<NavigationNotLoadedException>(() => a.Address);
        Assert.Equal("Springfield", b.Address.City);
    }
}
