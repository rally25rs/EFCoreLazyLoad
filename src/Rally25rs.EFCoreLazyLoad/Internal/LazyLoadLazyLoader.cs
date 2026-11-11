using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     EF Core <see cref="LazyLoader" /> that applies <see cref="LazyLoadOptions" />, with any per-entity
///     <see cref="LazyLoadAttribute" /> overrides (log / throw), before deferring to
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

    public override void Load(object entity, string navigationName = "")
    {
        if (!TryGetSettings(entity, navigationName, out var settings) || !TryBegin(navigationName))
        {
            base.Load(entity!, navigationName);
            return;
        }

        try
        {
            BeforeLoad(entity, navigationName, settings);
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
        if (!TryGetSettings(entity, navigationName, out var settings) || !TryBegin(navigationName))
        {
            await base.LoadAsync(entity!, cancellationToken, navigationName).ConfigureAwait(false);
            return;
        }

        try
        {
            BeforeLoad(entity, navigationName, settings);
            await base.LoadAsync(entity, cancellationToken, navigationName).ConfigureAwait(false);
        }
        finally
        {
            End(navigationName);
        }
    }

    /// <summary>
    ///     The context's options with the navigation's <see cref="LazyLoadAttribute" /> overrides applied. Returns
    ///     <see langword="false" /> when the load needs none of our handling and can go straight to EF.
    /// </summary>
    private bool TryGetSettings(object? entity, string navigationName, out EffectiveSettings settings)
    {
        if (entity is null || string.IsNullOrEmpty(navigationName))
        {
            settings = default;
            return false;
        }

        settings = EffectiveSettings.From(options, NavigationSettings.GetOverrides(entity.GetType(), navigationName));
        return settings.Intercepts;
    }

    private void BeforeLoad(object entity, string navigationName, EffectiveSettings settings)
    {
        if (!WouldLoad(entity, navigationName))
        {
            return;
        }

        var entityType = entity.GetType();
        var blocked = settings.Mode == LazyLoadMode.Throw;
        if (settings.LogLazyLoads)
        {
            WriteLog(entityType, navigationName, settings, blocked);
        }

        if (blocked)
        {
            throw new NavigationNotLoadedException(entityType, navigationName);
        }
    }

    private void WriteLog(Type entityType, string navigationName, EffectiveSettings settings, bool blocked)
    {
        var logger = loggerAccessor();
        if (!logger.IsEnabled(settings.LogLevel))
        {
            return; // don't pay for a stack trace nobody will see
        }

        var stackTrace = settings.LogStackTrace
            ? new LazyLoadStackTrace(entityType, navigationName, CallStack.Capture())
            : null;
        var displayName = TypeNames.Display(entityType);

        if (blocked)
        {
            Log.LazyLoadBlocked(logger, settings.LogLevel, stackTrace, displayName, navigationName);
        }
        else
        {
            Log.LazyLoad(logger, settings.LogLevel, stackTrace, displayName, navigationName);
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
