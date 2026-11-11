using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     The settings a <see cref="LazyLoadAttribute" /> overrides for one navigation of one CLR type. A
///     <see langword="null" /> member means "not overridden; use the context's <see cref="LazyLoadOptions" />".
/// </summary>
internal sealed record NavigationOverrides(
    LazyLoadMode? Mode,
    bool? LogLazyLoads,
    LogLevel? LogLevel,
    bool? LogStackTrace = null)
{
    public static readonly NavigationOverrides None = new(null, null, null);

    public bool IsEmpty
        => Mode is null && LogLazyLoads is null && LogLevel is null && LogStackTrace is null;
}

/// <summary>The effective settings for one lazy load: the context's options with any attribute overrides applied.</summary>
internal readonly record struct EffectiveSettings(
    LazyLoadMode Mode,
    bool LogLazyLoads,
    LogLevel LogLevel,
    bool LogStackTrace = true)
{
    /// <summary>Whether a lazy load needs any of our handling (otherwise it can go straight to EF).</summary>
    public bool Intercepts
        => Mode == LazyLoadMode.Throw || LogLazyLoads;

    public static EffectiveSettings From(LazyLoadOptions options, NavigationOverrides overrides)
        => new(
            overrides.Mode ?? options.Mode,
            overrides.LogLazyLoads ?? options.LogLazyLoads,
            overrides.LogLevel ?? options.LogLevel,
            overrides.LogStackTrace ?? options.LogStackTrace);
}

/// <summary>
///     Resolves the <see cref="LazyLoadAttribute" /> overrides for a navigation, cached per (CLR type, navigation).
/// </summary>
/// <remarks>
///     Each setting is resolved independently, first match wins: the attribute on the navigation property, then the
///     attribute on the entity's runtime type, then on each of its base types in turn.
/// </remarks>
internal static class NavigationSettings
{
    private const BindingFlags DeclaredInstanceMembers
        = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type EntityType, string NavigationName), NavigationOverrides> Cache
        = new();

    public static NavigationOverrides GetOverrides(Type entityType, string navigationName)
        => Cache.GetOrAdd((entityType, navigationName), static key => Resolve(key.EntityType, key.NavigationName));

    private static NavigationOverrides Resolve(Type entityType, string navigationName)
    {
        // Nearest first: the navigation property, then the classes from the runtime type up to the root.
        var attributes = new List<LazyLoadAttribute>();
        if (FindProperty(entityType, navigationName)?.GetCustomAttribute<LazyLoadAttribute>() is { } propertyAttribute)
        {
            attributes.Add(propertyAttribute);
        }

        for (var type = entityType; type is not null; type = type.BaseType)
        {
            if (type.GetCustomAttribute<LazyLoadAttribute>(inherit: false) is { } classAttribute)
            {
                attributes.Add(classAttribute);
            }
        }

        if (attributes.Count == 0)
        {
            return NavigationOverrides.None;
        }

        var overrides = new NavigationOverrides(
            attributes.Select(a => a.ModeOverride).FirstOrDefault(v => v is not null),
            attributes.Select(a => a.LogLazyLoadsOverride).FirstOrDefault(v => v is not null),
            attributes.Select(a => a.LogLevelOverride).FirstOrDefault(v => v is not null),
            attributes.Select(a => a.LogStackTraceOverride).FirstOrDefault(v => v is not null));

        return overrides.IsEmpty ? NavigationOverrides.None : overrides;
    }

    /// <summary>The most-derived declaration of the property (so a <c>new</c> property hides the base one).</summary>
    private static PropertyInfo? FindProperty(Type entityType, string navigationName)
    {
        for (var type = entityType; type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(navigationName, DeclaredInstanceMembers);
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }
}
