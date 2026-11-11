using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>
///     <see cref="LazyLoadOptions.LogStackTrace" />: lazy-load log entries carry the call stack of the code that read
///     the navigation, as a <see cref="LazyLoadStackTrace" /> exception.
/// </summary>
public class StackTraceLoggingTests(TestDatabaseFixture db)
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    // Reading the navigation from a separate (non-inlined) method gives the stack trace a known caller frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Address ReadAddress(Customer customer)
        => customer.Address;

    private static async Task<Customer> LoadCustomerAsync(Support.TestHost host)
        => await host.Context.Customers.SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

    [Fact]
    public async Task ByDefault_LogEntryCarriesStackTrace_StartingAtTheNavigationGetter()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await LoadCustomerAsync(host);

        ReadAddress(customer);

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        var trace = Assert.IsType<LazyLoadStackTrace>(entry.Exception);
        Assert.Equal(typeof(Customer), trace.EntityType);
        Assert.Equal(nameof(Customer.Address), trace.NavigationName);

        var frames = trace.StackTrace.Split(Environment.NewLine);
        Assert.Contains("Customer.get_Address()", frames[0], StringComparison.Ordinal);
        Assert.Contains($"StackTraceLoggingTests.{nameof(ReadAddress)}(", frames[1], StringComparison.Ordinal);
        Assert.Contains(nameof(ByDefault_LogEntryCarriesStackTrace_StartingAtTheNavigationGetter), trace.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StackTrace_LeavesOutEfCoreAndLibraryFrames()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await LoadCustomerAsync(host);

        ReadAddress(customer);

        var trace = Assert.IsType<LazyLoadStackTrace>(Assert.Single(host.Logs.LazyLoadEntries).Exception);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.", trace.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("Rally25rs.EFCoreLazyLoad.Internal.", trace.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StackTrace_IncludesFileAndLineNumbers()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await LoadCustomerAsync(host);

        ReadAddress(customer);

        var trace = Assert.Single(host.Logs.LazyLoadEntries).Exception!;
        Assert.Contains($"{nameof(StackTraceLoggingTests)}.cs:line ", trace.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StackTrace_RendersLikeAnExceptionInLogProviders()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await LoadCustomerAsync(host);

        ReadAddress(customer);

        // Log providers typically write exception.ToString(): type, message, then the stack trace.
        var rendered = Assert.Single(host.Logs.LazyLoadEntries).Exception!.ToString();
        Assert.StartsWith(
            "Rally25rs.EFCoreLazyLoad.LazyLoadStackTrace: The navigation 'Customer.Address' was accessed from:",
            rendered,
            StringComparison.Ordinal);
        Assert.Contains("Customer.get_Address()", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowMode_BlockedLoadLogEntry_CarriesStackTrace()
    {
        await using var host = db.CreateHost(o =>
        {
            o.Mode = LazyLoadMode.Throw;
            o.LogLazyLoads = true;
        });
        var customer = await LoadCustomerAsync(host);

        Assert.Throws<NavigationNotLoadedException>(() => ReadAddress(customer));

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Equal("LazyLoadBlocked", entry.EventId.Name);
        var trace = Assert.IsType<LazyLoadStackTrace>(entry.Exception);
        Assert.Contains(nameof(ReadAddress), trace.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogStackTraceDisabled_LogEntryHasNoException()
    {
        await using var host = db.CreateHost(o =>
        {
            o.LogLazyLoads = true;
            o.LogStackTrace = false;
        });
        var customer = await LoadCustomerAsync(host);

        ReadAddress(customer);

        Assert.Null(Assert.Single(host.Logs.LazyLoadEntries).Exception);
    }

    [Fact]
    public async Task AttributeLogStackTraceFalse_OverridesContextDefault()
    {
        // Product is [LazyLoad(LogLazyLoads = true, ..., LogStackTrace = false)].
        await using var host = db.CreateHost();
        var product = await host.Context.Products.FirstAsync(Ct);

        _ = product.Supplier;

        var entry = Assert.Single(host.Logs.LazyLoadEntries);
        Assert.Contains("Product.Supplier", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task IncludedNavigation_LogsNothing()
    {
        await using var host = db.CreateHost(o => o.LogLazyLoads = true);
        var customer = await host.Context.Customers
            .Include(c => c.Address)
            .SingleAsync(c => c.Id == TestDatabaseFixture.CustomerWithHobbiesId, Ct);

        ReadAddress(customer);

        Assert.Empty(host.Logs.LazyLoadEntries);
    }
}
