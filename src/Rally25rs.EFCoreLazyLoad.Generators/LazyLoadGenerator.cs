using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rally25rs.EFCoreLazyLoad.Generators;

/// <summary>
/// Implements partial navigation properties of <c>[LazyLoad]</c> entity types with getters that
/// call EF Core's <c>ILazyLoader</c>, and adds the single <c>ILazyLoader</c> member EF Core needs
/// to inject a loader into each entity instance.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LazyLoadGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name of the attribute (defined in the runtime library, matched by name only).</summary>
    public const string AttributeMetadataName = "Rally25rs.EFCoreLazyLoad.LazyLoadAttribute";

    /// <summary>Tracking names for incremental-step assertions in tests.</summary>
    public static class TrackingNames
    {
        public const string ClassModels = nameof(ClassModels);
        public const string PropertyModels = nameof(PropertyModels);
        public const string TypeModels = nameof(TypeModels);
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                    or Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax,
                static (ctx, ct) => ModelBuilder.Build((INamedTypeSymbol)ctx.TargetSymbol, ctx.SemanticModel.Compilation, ct))
            .WithTrackingName(TrackingNames.ClassModels);

        var propertyModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax,
                static (ctx, ct) => ModelBuilder.Build(((IPropertySymbol)ctx.TargetSymbol).ContainingType, ctx.SemanticModel.Compilation, ct))
            .WithTrackingName(TrackingNames.PropertyModels);

        // A type can be reached several times (attribute on the class, on several properties, on
        // several partial declarations). Models are built from the type symbol alone, so duplicates
        // are equal and can be collapsed by hint name.
        var typeModels = classModels.Collect()
            .Combine(propertyModels.Collect())
            .SelectMany(static (pair, _) => pair.Left.Concat(pair.Right)
                .GroupBy(static m => m.HintName)
                .Select(static g => g.First())
                .ToImmutableArray())
            .WithTrackingName(TrackingNames.TypeModels);

        context.RegisterSourceOutput(typeModels, static (spc, model) =>
        {
            foreach (var diagnostic in model.Diagnostics)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    GetDescriptor(diagnostic.Id),
                    diagnostic.Location?.ToLocation(),
                    diagnostic.MessageArgs.Items.Cast<object>().ToArray()));
            }

            if (model.CanEmit)
            {
                spc.AddSource(model.HintName, SourceEmitter.Emit(model));
            }
        });
    }

    private static DiagnosticDescriptor GetDescriptor(string id) => id switch
    {
        "EFLL001" => Diagnostics.TypeNotPartial,
        "EFLL002" => Diagnostics.PropertyNotPartial,
        "EFLL003" => Diagnostics.PropertyAlreadyImplemented,
        "EFLL004" => Diagnostics.PropertyNotReferenceType,
        "EFLL005" => Diagnostics.PropertyHasNoGetter,
        "EFLL006" => Diagnostics.PropertyNotInstance,
        "EFLL007" => Diagnostics.InheritedLoaderInaccessible,
        "EFLL009" => Diagnostics.BackingFieldIncompatible,
        "EFLL010" => Diagnostics.BackingFieldPropertyNotPartial,
        _ => Diagnostics.LanguageVersionTooLow,
    };
}
