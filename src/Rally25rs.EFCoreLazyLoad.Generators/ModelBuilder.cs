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
            var model = BuildProperty(property, classLevel, diagnostics);
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

    private static PropertyModel? BuildProperty(IPropertySymbol property, bool classLevel, List<DiagnosticInfo> diagnostics)
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

        return new PropertyModel(
            // Copied verbatim (includes `partial`); both parts must agree on accessibility,
            // required, virtual/override/sealed/new, etc.
            string.Join(" ", syntax.Modifiers.Select(m => m.Text)),
            property.Type.ToDisplayString(TypeFormat),
            EscapeIdentifier(property.Name),
            property.Type.NullableAnnotation == NullableAnnotation.Annotated,
            getter,
            setter);
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

    private static bool IsLazyLoader(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: "ILazyLoader", TypeKind: TypeKind.Interface } named
            && named.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore.Infrastructure";

    private static bool HasLazyLoadAttribute(ISymbol symbol)
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
