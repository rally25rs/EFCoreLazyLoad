namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     What happens when a navigation that was not loaded up front is accessed.
/// </summary>
public enum LazyLoadMode
{
    /// <summary>
    ///     Load the navigation from the database on first access (lazy loading).
    /// </summary>
    Load,

    /// <summary>
    ///     Throw a <see cref="NavigationNotLoadedException" /> instead of querying the database.
    ///     Use this to find navigations that should have been loaded with <c>Include</c>.
    /// </summary>
    Throw,
}
