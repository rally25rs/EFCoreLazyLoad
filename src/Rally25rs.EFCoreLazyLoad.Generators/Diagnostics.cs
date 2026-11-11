using Microsoft.CodeAnalysis;

namespace Rally25rs.EFCoreLazyLoad.Generators;

internal static class Diagnostics
{
    private const string Category = "EFCoreLazyLoad";

    public static readonly DiagnosticDescriptor TypeNotPartial = new(
        "EFLL001",
        "Type using [LazyLoad] must be a partial class",
        "Type '{0}' uses [LazyLoad] and must be a partial class; any containing types must also be partial",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNotPartial = new(
        "EFLL002",
        "[LazyLoad] property must be partial",
        "Property '{0}' is marked [LazyLoad] and must be declared partial without an implementation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyAlreadyImplemented = new(
        "EFLL003",
        "[LazyLoad] property already has an implementation",
        "Property '{0}' is marked [LazyLoad] but already has an implementing declaration; remove the implementation so the lazy-loading getter can be generated",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNotReferenceType = new(
        "EFLL004",
        "Lazy-loaded property must have a reference type",
        "Property '{0}' has type '{1}', which is not a reference type; only reference-type navigations can be lazy-loaded",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyHasNoGetter = new(
        "EFLL005",
        "Lazy-loaded property must have a getter",
        "Property '{0}' has no getter; lazy loading requires a get accessor",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNotInstance = new(
        "EFLL006",
        "Lazy-loaded property must be an instance, non-indexer property",
        "Property '{0}' is static or an indexer and cannot be lazy-loaded",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InheritedLoaderInaccessible = new(
        "EFLL007",
        "Inherited ILazyLoader member is not accessible",
        "Type '{0}' inherits ILazyLoader member '{1}' that is not accessible from it; EF Core requires a single ILazyLoader member per hierarchy, so make it protected",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LanguageVersionTooLow = new(
        "EFLL008",
        "[LazyLoad] requires C# 14 or later",
        "Type '{0}' uses [LazyLoad], which generates code using the C# 14 'field' keyword; set <LangVersion> to 14 or later",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BackingFieldIncompatible = new(
        "EFLL009",
        "Backing field type is not compatible with the lazy-loaded property",
        "Property '{0}' is backed by field '{1}' of type '{2}', which cannot be converted to the property type '{3}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BackingFieldPropertyNotPartial = new(
        "EFLL010",
        "Collection over a backing field will not be lazy-loaded",
        "Property '{0}' exposes backing field '{1}' but is not partial, so it will not be lazy-loaded; declare it as 'partial' with only a 'get;' accessor and the generator will implement it",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
