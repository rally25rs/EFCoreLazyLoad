namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Carries the call stack of the code that read an unloaded navigation. It is attached as the exception of the
///     lazy-load log entries when <see cref="LazyLoadOptions.LogStackTrace" /> is enabled, so that log providers
///     render the stack trace with the entry. It is never thrown.
/// </summary>
/// <remarks>
///     The stack trace starts at the navigation property's getter: frames from EF Core and from this package are
///     left out. File names and line numbers are included when debugging symbols (PDB files) are available.
/// </remarks>
public sealed class LazyLoadStackTrace : Exception
{
    private readonly string _stackTrace;

    internal LazyLoadStackTrace(Type entityType, string navigationName, string stackTrace)
        : base($"The navigation '{Internal.TypeNames.Display(entityType)}.{navigationName}' was accessed from:")
    {
        EntityType = entityType;
        NavigationName = navigationName;
        _stackTrace = stackTrace;
    }

    /// <summary>The CLR type of the entity whose navigation was accessed.</summary>
    public Type EntityType { get; }

    /// <summary>The name of the navigation that was accessed.</summary>
    public string NavigationName { get; }

    /// <summary>The call stack of the code that accessed the navigation, starting at the navigation's getter.</summary>
    public override string StackTrace
        => _stackTrace;
}
