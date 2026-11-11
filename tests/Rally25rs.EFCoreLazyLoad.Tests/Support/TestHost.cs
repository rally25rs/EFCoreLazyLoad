using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests.Support;

/// <summary>
///     A per-test application: a fresh <see cref="ServiceCollection" /> with logging captured in <see cref="Logs" />,
///     a <see cref="TestDbContext" /> registration whose SQL is captured in <see cref="Sql" />, and a default scope
///     whose context is <see cref="Context" />.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private IServiceScope? _scope;

    internal TestHost(string connectionString, Action<IServiceCollection, Action<DbContextOptionsBuilder>> register)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        register(services, o => o.UseSqlite(connectionString).AddInterceptors(Sql));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public SqlCapture Sql { get; } = new();

    public LogCapture Logs { get; } = new();

    public IServiceProvider Services
        => _services;

    /// <summary>The context of the host's default scope (created on first use).</summary>
    public TestDbContext Context
        => (_scope ??= _services.CreateScope()).ServiceProvider.GetRequiredService<TestDbContext>();

    /// <summary>A new scope, e.g. to get another (or, when pooled, a reused) context instance.</summary>
    public IServiceScope CreateScope()
        => _services.CreateScope();

    public async ValueTask DisposeAsync()
    {
        if (_scope is IAsyncDisposable scope)
        {
            await scope.DisposeAsync();
        }

        await _services.DisposeAsync();
    }
}
