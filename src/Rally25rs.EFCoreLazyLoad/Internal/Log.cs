using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     Log messages written by Rally25rs.EFCoreLazyLoad. Event ids are stable and part of the observable contract.
/// </summary>
internal static partial class Log
{
    public const string CategoryName = "Rally25rs.EFCoreLazyLoad";

    [LoggerMessage(
        EventId = 1,
        EventName = "LazyLoad",
        Message = "Lazy-loading navigation '{EntityType}.{NavigationName}'. Consider loading it up front with .Include() in the query that loaded the entity.")]
    public static partial void LazyLoad(ILogger logger, LogLevel level, Exception? stackTrace, string entityType, string navigationName);

    [LoggerMessage(
        EventId = 2,
        EventName = "LazyLoadBlocked",
        Message = "Blocked lazy-load of navigation '{EntityType}.{NavigationName}' because LazyLoadMode is Throw. Load it up front with .Include() in the query that loaded the entity.")]
    public static partial void LazyLoadBlocked(ILogger logger, LogLevel level, Exception? stackTrace, string entityType, string navigationName);
}
