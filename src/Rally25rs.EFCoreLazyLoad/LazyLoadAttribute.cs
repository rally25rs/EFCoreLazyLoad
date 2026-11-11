using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Marks an entity class, or individual navigation properties of it, for lazy loading, and optionally overrides
///     the context's <see cref="LazyLoadOptions" /> for them.
///     The Rally25rs.EFCoreLazyLoad source generator emits the implementation of each marked
///     <c>partial</c> property so that its getter asks EF Core's <c>ILazyLoader</c> to load the navigation
///     on first access.
/// </summary>
/// <remarks>
///     <para>
///         On a class, every <c>partial</c> property declaration that has no implementation part gets a lazy-loading
///         getter. On a property, only that property does. In both cases the class must be <c>partial</c>.
///     </para>
///     <para>
///         Only reference-type properties (reference and collection navigations) can be lazy loaded.
///     </para>
///     <para>
///         How a lazy load behaves (load, log, or throw) is configured for the context with
///         <see cref="LazyLoadDbContextOptionsBuilderExtensions.UseLazyLoad(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, System.Action{LazyLoadOptions}?)" />.
///         <see cref="Mode" />, <see cref="LogLazyLoads" />, <see cref="LogLevel" /> and <see cref="LogStackTrace" /> override those settings for
///         the marked entity or navigation. Each one is applied only when it is set; settings that are not set use
///         the context's value. When several attributes set the same setting, the one on the navigation property wins,
///         then the one on the nearest class in the entity's inheritance hierarchy.
///     </para>
///     <para>
///         Without <c>UseLazyLoad</c>, marked navigations get EF Core's standard lazy-loading behavior and these
///         settings are ignored.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class LazyLoadAttribute : Attribute
{
    private LazyLoadMode? _mode;
    private bool? _logLazyLoads;
    private LogLevel? _logLevel;
    private bool? _logStackTrace;

    /// <summary>
    ///     Overrides <see cref="LazyLoadOptions.Mode" /> for the marked entity or navigation.
    ///     When not set, the context's mode is used (reading this property then returns <see cref="LazyLoadMode.Load" />).
    /// </summary>
    public LazyLoadMode Mode
    {
        get => _mode ?? LazyLoadMode.Load;
        set => _mode = value;
    }

    /// <summary>
    ///     Overrides <see cref="LazyLoadOptions.LogLazyLoads" /> for the marked entity or navigation.
    ///     When not set, the context's setting is used (reading this property then returns <see langword="false" />).
    /// </summary>
    public bool LogLazyLoads
    {
        get => _logLazyLoads ?? false;
        set => _logLazyLoads = value;
    }

    /// <summary>
    ///     Overrides <see cref="LazyLoadOptions.LogLevel" /> for the marked entity or navigation.
    ///     When not set, the context's level is used (reading this property then returns
    ///     <see cref="Microsoft.Extensions.Logging.LogLevel.Warning" />).
    /// </summary>
    public LogLevel LogLevel
    {
        get => _logLevel ?? LogLevel.Warning;
        set => _logLevel = value;
    }

    /// <summary>
    ///     Overrides <see cref="LazyLoadOptions.LogStackTrace" /> for the marked entity or navigation.
    ///     When not set, the context's setting is used (reading this property then returns <see langword="true" />).
    /// </summary>
    public bool LogStackTrace
    {
        get => _logStackTrace ?? true;
        set => _logStackTrace = value;
    }

    internal LazyLoadMode? ModeOverride
        => _mode;

    internal bool? LogLazyLoadsOverride
        => _logLazyLoads;

    internal LogLevel? LogLevelOverride
        => _logLevel;

    internal bool? LogStackTraceOverride
        => _logStackTrace;
}
