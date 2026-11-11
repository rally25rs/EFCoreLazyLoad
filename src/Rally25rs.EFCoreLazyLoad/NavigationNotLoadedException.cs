using Rally25rs.EFCoreLazyLoad.Internal;

namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Thrown in <see cref="LazyLoadMode.Throw" /> mode when a navigation is accessed that was not loaded,
///     instead of lazy loading it. No database query is executed.
/// </summary>
public class NavigationNotLoadedException : InvalidOperationException
{
    /// <summary>
    ///     Creates a new <see cref="NavigationNotLoadedException" />.
    /// </summary>
    /// <param name="entityType">The CLR type of the entity whose navigation was accessed.</param>
    /// <param name="navigationName">The name of the navigation that was accessed.</param>
    public NavigationNotLoadedException(Type entityType, string navigationName)
        : base(CreateMessage(entityType, navigationName))
    {
        EntityType = entityType;
        NavigationName = navigationName;
    }

    /// <summary>
    ///     The CLR type of the entity whose navigation was accessed.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    ///     The name of the navigation that was accessed.
    /// </summary>
    public string NavigationName { get; }

    private static string CreateMessage(Type entityType, string navigationName)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrEmpty(navigationName);

        return $"The navigation '{TypeNames.Display(entityType)}.{navigationName}' was accessed but not loaded. "
            + $"Add .Include(e => e.{navigationName}) to the query that loaded the entity, load it explicitly, "
            + $"or use {nameof(LazyLoadMode)}.{nameof(LazyLoadMode.Load)} to allow lazy loading.";
    }
}
