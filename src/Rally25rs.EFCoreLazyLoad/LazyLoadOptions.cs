using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Options that control how navigations marked with <see cref="LazyLoadAttribute" /> behave when they are
///     accessed without having been loaded.
/// </summary>
public sealed class LazyLoadOptions
{
    /// <summary>
    ///     Whether an unloaded navigation is loaded on access or causes an exception.
    ///     The default is <see cref="LazyLoadMode.Load" />.
    /// </summary>
    public LazyLoadMode Mode { get; set; } = LazyLoadMode.Load;

    /// <summary>
    ///     When <see langword="true" />, a log entry is written each time a navigation is lazy loaded
    ///     (or, in <see cref="LazyLoadMode.Throw" /> mode, each time a lazy load is blocked).
    ///     The default is <see langword="false" />.
    /// </summary>
    public bool LogLazyLoads { get; set; }

    /// <summary>
    ///     The level used for the entries written when <see cref="LogLazyLoads" /> is enabled.
    ///     The default is <see cref="Microsoft.Extensions.Logging.LogLevel.Warning" />.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    ///     An optional logger factory to write lazy-load log entries to. When <see langword="null" /> (the default),
    ///     the <see cref="ILoggerFactory" /> that EF Core uses for the context is used, which is the application's
    ///     logger factory when the context is registered with <c>AddDbContext</c>.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    internal LazyLoadOptions Clone()
        => new()
        {
            Mode = Mode,
            LogLazyLoads = LogLazyLoads,
            LogLevel = LogLevel,
            LoggerFactory = LoggerFactory,
        };
}
