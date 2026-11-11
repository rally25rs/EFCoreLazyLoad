using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     Replacement for EF Core's <c>LazyLoaderFactory</c> (scoped to a context instance). Mirrors its lifetime
///     handling: loaders are disposed when the context is disposed or returned to a pool.
/// </summary>
internal sealed class LazyLoadLazyLoaderFactory : ILazyLoaderFactory
{
    private readonly ICurrentDbContext _currentContext;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _efLogger;
    private readonly LazyLoadOptions _options;
    private readonly ILoggerFactory _contextLoggerFactory;
    private readonly List<ILazyLoader> _loaders = [];
    private ILogger? _logger;

    public LazyLoadLazyLoaderFactory(
        ICurrentDbContext currentContext,
        IDiagnosticsLogger<DbLoggerCategory.Infrastructure> efLogger,
        IDbContextOptions contextOptions,
        ILoggerFactory loggerFactory)
    {
        _currentContext = currentContext;
        _efLogger = efLogger;
        _contextLoggerFactory = loggerFactory;
        _options = contextOptions.FindExtension<LazyLoadOptionsExtension>()?.Options ?? new LazyLoadOptions();
    }

    /// <summary>Created on first use so contexts that never log never create a logger.</summary>
    private ILogger Logger
        => _logger ??= (_options.LoggerFactory ?? _contextLoggerFactory).CreateLogger(Log.CategoryName);

    public ILazyLoader Create()
    {
        var loader = new LazyLoadLazyLoader(_currentContext, _efLogger, _options, () => Logger);
        _loaders.Add(loader);
        return loader;
    }

    public void Dispose()
    {
        foreach (var loader in _loaders)
        {
            loader.Dispose();
        }

        _loaders.Clear();
    }

    public void ResetState()
        => Dispose();

    public Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        Dispose();
        return Task.CompletedTask;
    }
}
