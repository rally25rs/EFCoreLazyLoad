using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rally25rs.EFCoreLazyLoad.Generators;

/// <summary>
///     Reports EFLL010: a hand-written, get-only collection property over a backing field in a class-level
///     <c>[LazyLoad]</c> class (<c>IEnumerable&lt;T&gt; Items =&gt; _items.AsReadOnly()</c>). It compiles and works with
///     <c>Include</c>, but silently never lazy-loads, nor throws in Throw mode.
/// </summary>
/// <remarks>
///     This is an analyzer rather than a generator diagnostic so that <c>#pragma warning disable</c> works on it:
///     the compiler doesn't apply pragmas to diagnostics reported by source generators.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LazyLoadAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = [Diagnostics.BackingFieldPropertyNotPartial];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var syntax = (PropertyDeclarationSyntax)context.Node;
        if (syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
            || context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not { } property
            || property.IsStatic || property.IsIndexer || property.GetMethod is null || property.SetMethod is not null
            || !ModelBuilder.HasLazyLoadAttribute(property.ContainingType)
            || ModelBuilder.HasLazyLoadAttribute(property) // EFLL002 (from the generator) covers this one.
            || !ModelBuilder.IsEntityCollection(property.Type)
            || ModelBuilder.FindBackingField(property) is not { } field
            || !ModelBuilder.IsEntityCollection(field.Type)
            || CallsLazyLoader(syntax, context.SemanticModel, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.BackingFieldPropertyNotPartial,
            syntax.Identifier.GetLocation(),
            property.ToDisplayString(),
            field.Name));
    }

    /// <summary>
    ///     Whether the getter asks an <c>ILazyLoader</c> to load (<c>LazyLoader?.Load(this, nameof(Items))</c>): a
    ///     hand-written lazy-loading getter, for example one that copies the list under a lock.
    /// </summary>
    private static bool CallsLazyLoader(
        PropertyDeclarationSyntax syntax, SemanticModel semanticModel, SyntaxNodeAnalysisContext context)
        => syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
            semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && (ModelBuilder.IsLazyLoader(method.ContainingType)
                || method.ContainingType is
                {
                    Name: "LazyLoaderExtensions",
                    ContainingNamespace: { Name: "Infrastructure", ContainingNamespace.Name: "EntityFrameworkCore" },
                }));
}
