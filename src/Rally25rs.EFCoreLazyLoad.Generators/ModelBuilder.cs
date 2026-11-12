using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rally25rs.EFCoreLazyLoad.Generators;

/// <summary>Turns an entity type symbol into an equatable <see cref="TypeModel"/>.</summary>
internal static class ModelBuilder
{
    /// <summary>Name of the loader field the generator emits.</summary>
    public const string LoaderFieldName = "__lazyLoader";

    private const int CSharp14 = 1400; // LanguageVersion.CSharp14; numeric so older Roslyn APIs compile.

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static TypeModel Build(INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var typeLocation = type.Locations.FirstOrDefault();
        var hintName = GetHintName(type);
        var containingTypes = new List<ContainingTypeModel>();

        TypeModel Fail() => new(
            hintName, null, containingTypes.ToEquatableArray(), "class", type.Name, null, LoaderFieldName,
            Enumerable.Empty<PropertyModel>().ToEquatableArray(), diagnostics.ToEquatableArray())
        { CanEmit = false };

        // The type and every containing type must be partial; the entity itself must be a class.
        if (type.TypeKind != TypeKind.Class || !IsPartial(type))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.TypeNotPartial, typeLocation, type.ToDisplayString()));
            return Fail();
        }

        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            if (!IsPartial(outer))
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.TypeNotPartial, typeLocation, type.ToDisplayString()));
                return Fail();
            }

            containingTypes.Insert(0, new ContainingTypeModel(GetKeyword(outer), GetNameWithTypeParameters(outer)));
        }

        var syntaxTree = type.DeclaringSyntaxReferences.First().SyntaxTree;
        if (syntaxTree.Options is CSharpParseOptions parseOptions && (int)parseOptions.LanguageVersion < CSharp14)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.LanguageVersionTooLow, typeLocation, type.ToDisplayString()));
            return Fail();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var classLevel = HasLazyLoadAttribute(type);
        var properties = new List<PropertyModel>();
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            var model = BuildProperty(property, classLevel, compilation, diagnostics);
            if (model is not null)
            {
                properties.Add(model);
            }
        }

        // Loader member resolution. EF Core's ServicePropertyDiscoveryConvention requires exactly
        // one ILazyLoader member per entity hierarchy, so:
        //  1. A user-declared ILazyLoader field/property on the type or any base type is reused.
        //  2. Otherwise, if a base type in this compilation also uses [LazyLoad], the topmost such
        //     base emits the field and every derived type reuses it.
        //  3. Otherwise this type emits the field.
        // The emitted field is `protected` (so derived types, including ones in other assemblies,
        // can reach it) unless the type is sealed, where it is `private` to avoid CS0628.
        string? fieldAccessibility = null;
        var loaderName = LoaderFieldName;
        var existing = FindLoaderMember(type);
        if (existing is not null)
        {
            if (!SymbolEqualityComparer.Default.Equals(existing.ContainingType, type)
                && !compilation.IsSymbolAccessibleWithin(existing, type))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.InheritedLoaderInaccessible, typeLocation, type.ToDisplayString(), existing.ToDisplayString()));
                return Fail();
            }

            loaderName = EscapeIdentifier(existing.Name);
        }
        else if (!HasLazyLoadBaseInCompilation(type, compilation))
        {
            fieldAccessibility = type.IsSealed ? "private" : "protected";
        }

        return new TypeModel(
            hintName,
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            containingTypes.ToEquatableArray(),
            GetKeyword(type),
            GetNameWithTypeParameters(type),
            fieldAccessibility,
            loaderName,
            properties.ToEquatableArray(),
            diagnostics.ToEquatableArray());
    }

    private static PropertyModel? BuildProperty(
        IPropertySymbol property, bool classLevel, Compilation compilation, List<DiagnosticInfo> diagnostics)
    {
        var explicitlyMarked = HasLazyLoadAttribute(property)
            || (property.PartialImplementationPart is { } impl && HasLazyLoadAttribute(impl));
        if (!classLevel && !explicitlyMarked)
        {
            return null;
        }

        var location = property.Locations.FirstOrDefault();
        var name = property.ToDisplayString();
        var isPartial = property.IsPartialDefinition || property.PartialDefinitionPart is not null;

        if (!isPartial)
        {
            // Class-level [LazyLoad] only applies to partial properties; ordinary ones (keys,
            // scalars) are left alone.
            // (EFLL010, the hand-written read-only collection pattern, is reported by LazyLoadAnalyzer so that it
            // can be suppressed with #pragma, which doesn't apply to generator diagnostics.)
            if (explicitlyMarked)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.PropertyNotPartial, location, name));
            }

            return null;
        }

        if (property.PartialImplementationPart is not null || !property.IsPartialDefinition)
        {
            // Hand-implemented partial properties are skipped under class-level [LazyLoad].
            if (explicitlyMarked)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.PropertyAlreadyImplemented, location, name));
            }

            return null;
        }

        if (property.IsStatic || property.IsIndexer)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.PropertyNotInstance, location, name));
            return null;
        }

        if (!property.Type.IsReferenceType)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.PropertyNotReferenceType, location, name, property.Type.ToDisplayString()));
            return null;
        }

        if (property.GetMethod is null)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.PropertyHasNoGetter, location, name));
            return null;
        }

        if (property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not PropertyDeclarationSyntax syntax
            || syntax.AccessorList is null)
        {
            return null;
        }

        AccessorModel? getter = null;
        AccessorModel? setter = null;
        foreach (var accessor in syntax.AccessorList.Accessors)
        {
            var modifiers = string.Join(" ", accessor.Modifiers.Select(m => m.Text));
            switch (accessor.Kind())
            {
                case SyntaxKind.GetAccessorDeclaration:
                    getter = new AccessorModel("get", modifiers);
                    break;
                case SyntaxKind.SetAccessorDeclaration:
                    setter = new AccessorModel("set", modifiers);
                    break;
                case SyntaxKind.InitAccessorDeclaration:
                    setter = new AccessorModel("init", modifiers);
                    break;
            }
        }

        if (getter is null)
        {
            return null;
        }

        // A get-only property over a declared backing field (the DDD read-only collection pattern):
        //     private readonly List<Line> _lines = [];
        //     public partial IReadOnlyCollection<Line> Lines { get; }
        // EF maps the navigation to the field by its naming convention and fills it; the getter only has to ask
        // the loader to load, then expose the field. It must not use `field`: EF prefers a compiler-generated
        // backing field over a convention-named one.
        string? backingFieldReturn = null;
        if (setter is null && FindBackingField(property) is { } backingField)
        {
            backingFieldReturn = GetBackingFieldReturn(property, backingField, compilation);
            if (backingFieldReturn is null)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.BackingFieldIncompatible,
                    location,
                    name,
                    backingField.Name,
                    backingField.Type.ToDisplayString(),
                    property.Type.ToDisplayString()));
                return null;
            }
        }

        return new PropertyModel(
            // Copied verbatim (includes `partial`); both parts must agree on accessibility,
            // required, virtual/override/sealed/new, etc.
            string.Join(" ", syntax.Modifiers.Select(m => m.Text)),
            property.Type.ToDisplayString(TypeFormat),
            EscapeIdentifier(property.Name),
            property.Type.NullableAnnotation == NullableAnnotation.Annotated,
            getter,
            setter,
            backingFieldReturn);
    }

    /// <summary>
    ///     The field EF Core's backing-field convention would match to the property: <c>_name</c>, <c>_Name</c>,
    ///     <c>m_name</c>, <c>m_Name</c>, then <c>name</c> (declared on the property's own type).
    /// </summary>
    internal static IFieldSymbol? FindBackingField(IPropertySymbol property)
    {
        var pascal = property.Name;
        var camel = char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        string[] candidates = ["_" + camel, "_" + pascal, "m_" + camel, "m_" + pascal, camel];
        foreach (var candidate in candidates)
        {
            if (candidate == pascal)
            {
                continue;
            }

            var field = property.ContainingType.GetMembers(candidate)
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f is { IsStatic: false, IsImplicitlyDeclared: false, IsConst: false });
            if (field is not null && field.Type.IsReferenceType)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>
    ///     What the getter returns: <c>this._f.AsReadOnly()</c> when the field type has a parameterless
    ///     <c>AsReadOnly()</c> (as <see cref="List{T}" /> does) that converts to the property type, so callers can't
    ///     cast the result back to the mutable list; otherwise <c>this._f</c> when it converts directly.
    ///     <see langword="null" /> when neither does.
    /// </summary>
    private static string? GetBackingFieldReturn(IPropertySymbol property, IFieldSymbol field, Compilation compilation)
    {
        var fieldName = "this." + EscapeIdentifier(field.Name);
        var fieldNullable = field.Type.NullableAnnotation == NullableAnnotation.Annotated;
        var suppress = fieldNullable && property.Type.NullableAnnotation != NullableAnnotation.Annotated ? "!" : "";

        var asReadOnly = field.Type.GetMembers("AsReadOnly")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m is { IsStatic: false, Parameters.Length: 0, TypeParameters.Length: 0, DeclaredAccessibility: Accessibility.Public });
        if (asReadOnly is not null && compilation.ClassifyConversion(asReadOnly.ReturnType, property.Type).IsImplicit)
        {
            return fieldName + (fieldNullable ? "?" : "") + ".AsReadOnly()" + suppress;
        }

        var conversion = compilation.ClassifyConversion(field.Type, property.Type);
        return conversion.IsImplicit && !conversion.IsUserDefined ? fieldName + suppress : null;
    }

    internal static bool IsEntityCollection(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        IEnumerable<INamedTypeSymbol> candidates = type is INamedTypeSymbol named
            ? type.AllInterfaces.Prepend(named)
            : type.AllInterfaces;
        return candidates.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            && i.TypeArguments[0] is { TypeKind: TypeKind.Class, SpecialType: not SpecialType.System_String });
    }

    private static ISymbol? FindLoaderMember(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                var memberType = member switch
                {
                    IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } f => f.Type,
                    IPropertySymbol { IsStatic: false } p => p.Type,
                    _ => null,
                };

                if (memberType is not null && IsLazyLoader(memberType))
                {
                    return member;
                }
            }
        }

        return null;
    }

    private static bool HasLazyLoadBaseInCompilation(INamedTypeSymbol type, Compilation compilation)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, compilation.Assembly)
                && (HasLazyLoadAttribute(current)
                    || current.GetMembers().OfType<IPropertySymbol>().Any(HasLazyLoadAttribute)))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsLazyLoader(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: "ILazyLoader", TypeKind: TypeKind.Interface } named
            && named.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore.Infrastructure";

    internal static bool HasLazyLoadAttribute(ISymbol symbol)
        => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == LazyLoadGenerator.AttributeMetadataName);

    private static bool IsPartial(INamedTypeSymbol type)
        => type.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax() is TypeDeclarationSyntax t && t.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static string GetKeyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    private static string GetNameWithTypeParameters(INamedTypeSymbol type)
    {
        var name = EscapeIdentifier(type.Name);
        return type.TypeParameters.Length == 0
            ? name
            : name + "<" + string.Join(", ", type.TypeParameters.Select(p => EscapeIdentifier(p.Name))) + ">";
    }

    private static string GetHintName(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        for (var current = type; current is not null; current = current.ContainingType)
        {
            parts.Insert(0, current.MetadataName);
        }

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            parts.Insert(0, type.ContainingNamespace.ToDisplayString());
        }

        var builder = new StringBuilder();
        foreach (var ch in string.Join(".", parts))
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '.' or '_' ? ch : '_');
        }

        return builder.Append(".LazyLoad.g.cs").ToString();
    }

    private static string EscapeIdentifier(string name)
        => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
