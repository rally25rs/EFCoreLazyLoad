using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Rally25rs.EFCoreLazyLoad.Generators.Tests;

/// <summary>Builds in-memory compilations and runs <see cref="LazyLoadGenerator"/> over them.</summary>
internal static class GeneratorHarness
{
    // Mirrors the attribute shipped in the runtime library; the generator matches it by name only.
    public const string AttributeSource = """
        namespace Rally25rs.EFCoreLazyLoad
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Property, Inherited = false)]
            public sealed class LazyLoadAttribute : global::System.Attribute { }
        }
        """;

    public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    public static CSharpCompilation CreateCompilation(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp14,
        IEnumerable<MetadataReference>? extraReferences = null,
        string assemblyName = "TestAssembly",
        bool includeAttribute = true)
    {
        var parseOptions = ParseOptions.WithLanguageVersion(languageVersion);
        SyntaxTree[] trees = includeAttribute
            ? [CSharpSyntaxTree.ParseText(AttributeSource, parseOptions, path: "LazyLoadAttribute.cs"), CSharpSyntaxTree.ParseText(source, parseOptions, path: "Entities.cs")]
            : [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Entities.cs")];
        return CSharpCompilation.Create(
            assemblyName,
            trees,
            References.AddRange(extraReferences ?? []),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    // Same defaults the .NET SDK applies (assembly unification noise).
                    ["CS1701"] = ReportDiagnostic.Suppress,
                    ["CS1702"] = ReportDiagnostic.Suppress,
                }));
    }

    public static GeneratorDriver CreateDriver(LanguageVersion languageVersion = LanguageVersion.CSharp14)
        => CSharpGeneratorDriver.Create(
            [new LazyLoadGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions.WithLanguageVersion(languageVersion),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    public static RunResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp14,
        IEnumerable<MetadataReference>? extraReferences = null,
        string assemblyName = "TestAssembly",
        bool includeAttribute = true)
    {
        var compilation = CreateCompilation(source, languageVersion, extraReferences, assemblyName, includeAttribute);
        var driver = CreateDriver(languageVersion)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics, TestContext.Current.CancellationToken);
        return new RunResult(driver.GetRunResult(), output, generatorDiagnostics);
    }

    /// <summary>Runs the generator and asserts that neither it nor the compiler reports anything.</summary>
    public static RunResult RunClean(
        string source,
        IEnumerable<MetadataReference>? extraReferences = null,
        string assemblyName = "TestAssembly",
        bool includeAttribute = true)
    {
        var result = Run(source, extraReferences: extraReferences, assemblyName: assemblyName, includeAttribute: includeAttribute);
        Assert.Empty(result.GeneratorDiagnostics);
        var problems = result.Output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();
        Assert.True(
            problems.Count == 0,
            "Compilation reported:\n" + string.Join("\n", problems) + "\n\nGenerated:\n" + result.AllGeneratedSource);
        return result;
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var framework = tpa.Where(p =>
        {
            var name = Path.GetFileName(p);
            return name.StartsWith("System.", StringComparison.Ordinal)
                || name is "System.dll" or "netstandard.dll" or "mscorlib.dll";
        });

        return framework
            .Append(typeof(ILazyLoader).Assembly.Location)
            .Distinct()
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
    }

    internal sealed record RunResult(GeneratorDriverRunResult DriverResult, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics)
    {
        public string AllGeneratedSource => string.Join(
            "\n// ----\n",
            DriverResult.GeneratedTrees.Select(t => t.FilePath + "\n" + t.GetText()));

        public string GeneratedSource(string hintNameSuffix)
            => DriverResult.Results.Single().GeneratedSources
                .Single(s => s.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal))
                .SourceText.ToString();
    }
}
