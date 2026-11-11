using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     EF Core <see cref="LazyLoader" /> that applies <see cref="LazyLoadOptions" /> (log / throw) before deferring to
///     the base implementation, which keeps EF's reentrancy guard, no-tracking support and disposed-context handling.
/// </summary>
internal sealed class LazyLoadLazyLoader(
    ICurrentDbContext currentContext,
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> efLogger,
    LazyLoadOptions options,
    Func<ILogger> loggerAccessor)
    : LazyLoader(currentContext, efLogger)
{
    private readonly Lock _inProgressLock = new();

    // Navigations of this loader's entity currently inside Load/LoadAsync. Calls that arrive while one is in
    // progress (recursion from within EF, or another thread) skip our checks and let base wait / dedupe.
    private HashSet<string>? _inProgress;

    private bool Intercepts
        => options.Mode == LazyLoadMode.Throw || options.LogLazyLoads;

    public override void Load(object entity, string navigationName = "")
    {
        if (!Intercepts || entity is null || string.IsNullOrEmpty(navigationName) || !TryBegin(navigationName))
        {
            base.Load(entity!, navigationName);
            return;
        }

        try
        {
            BeforeLoad(entity, navigationName);
            base.Load(entity, navigationName);
        }
        finally
        {
            End(navigationName);
        }
    }

    public override async Task LoadAsync(
        object entity,
        CancellationToken cancellationToken = default,
        string navigationName = "")
    {
        if (!Intercepts || entity is null || string.IsNullOrEmpty(navigationName) || !TryBegin(navigationName))
        {
            await base.LoadAsync(entity!, cancellationToken, navigationName).ConfigureAwait(false);
            return;
        }

        try
        {
            BeforeLoad(entity, navigationName);
            await base.LoadAsync(entity, cancellationToken, navigationName).ConfigureAwait(false);
        }
        finally
        {
            End(navigationName);
        }
    }

    private void BeforeLoad(object entity, string navigationName)
    {
        if (!WouldLoad(entity, navigationName))
        {
            return;
        }

        var entityType = entity.GetType();
        if (options.Mode == LazyLoadMode.Throw)
        {
            if (options.LogLazyLoads)
            {
                Log.LazyLoadBlocked(loggerAccessor(), options.LogLevel, TypeNames.Display(entityType), navigationName);
            }

            throw new NavigationNotLoadedException(entityType, navigationName);
        }

        if (options.LogLazyLoads)
        {
            Log.LazyLoad(loggerAccessor(), options.LogLevel, TypeNames.Display(entityType), navigationName);
        }
    }

    /// <summary>
    ///     Mirrors the conditions of EF's private <c>LazyLoader.ShouldLoad</c>. A disposed or detached loader has a
    ///     <see langword="null" /> context; that case is left to the base class, which reports
    ///     <c>LazyLoadOnDisposedContextWarning</c>.
    /// </summary>
    private bool WouldLoad(object entity, string navigationName)
    {
        var context = Context;
        if (context is null
            || IsLoaded(entity, navigationName)
            || !context.ChangeTracker.LazyLoadingEnabled)
        {
            return false;
        }

        var navigation = context.Entry(entity).Navigation(navigationName);
        return navigation.Metadata.LazyLoadingEnabled && !navigation.IsLoaded;
    }

    private bool TryBegin(string navigationName)
    {
        lock (_inProgressLock)
        {
            return (_inProgress ??= new HashSet<string>(StringComparer.Ordinal)).Add(navigationName);
        }
    }

    private void End(string navigationName)
    {
        lock (_inProgressLock)
        {
            _inProgress?.Remove(navigationName);
        }
    }
}
