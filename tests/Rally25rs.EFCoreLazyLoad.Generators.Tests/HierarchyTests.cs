using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rally25rs.EFCoreLazyLoad.Generators.Tests;

public class HierarchyTests
{
    private const string RelatedTypes = """
        public class Owner { public int Id { get; set; } }
        public class Toy { public int Id { get; set; } }
        public class Vet { public int Id { get; set; } }
        """;

    [Fact]
    public void BaseAndDerived_HaveExactlyOneLoaderMember()
    {
        var result = GeneratorHarness.RunClean($$"""
            using System.Collections.Generic;
            using Rally25rs.EFCoreLazyLoad;
            namespace Zoo;
            {{RelatedTypes}}

            [LazyLoad]
            public partial class Animal
            {
                public partial Owner Owner { get; set; }
            }

            [LazyLoad]
            public partial class Dog : Animal
            {
                public partial List<Toy> Toys { get; set; }
            }

            public partial class Puppy : Dog
            {
                [LazyLoad] public partial Vet Vet { get; set; }
            }
            """);

        foreach (var name in new[] { "Zoo.Animal", "Zoo.Dog", "Zoo.Puppy" })
        {
            var loaders = LoaderMembersInHierarchy(result.Output, name);
            var loader = Assert.Single(loaders);
            Assert.Equal("Zoo.Animal", loader.ContainingType.ToDisplayString());
            Assert.Equal(Accessibility.Protected, loader.DeclaredAccessibility);
        }

        Assert.DoesNotContain("ILazyLoader", result.GeneratedSource("Zoo.Dog.LazyLoad.g.cs"));
        Assert.DoesNotContain("ILazyLoader", result.GeneratedSource("Zoo.Puppy.LazyLoad.g.cs"));
        Assert.Contains("Load(__lazyLoader, this, ref field, nameof(Toys))", result.GeneratedSource("Zoo.Dog.LazyLoad.g.cs"));
    }

    [Fact]
    public void DerivedOnly_EmitsItsOwnLoader()
    {
        var result = GeneratorHarness.RunClean($$"""
            using Rally25rs.EFCoreLazyLoad;
            {{RelatedTypes}}

            public class Animal { public int Id { get; set; } }

            [LazyLoad]
            public sealed partial class Dog : Animal
            {
                public partial Owner Owner { get; set; }
            }
            """);

        var loader = Assert.Single(LoaderMembersInHierarchy(result.Output, "Dog"));
        Assert.Equal("Dog", loader.ContainingType.Name);
        Assert.Equal(Accessibility.Private, loader.DeclaredAccessibility);
    }

    [Fact]
    public void UserDeclaredLoaderInBase_IsReused()
    {
        var result = GeneratorHarness.RunClean($$"""
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Rally25rs.EFCoreLazyLoad;
            {{RelatedTypes}}

            public abstract class Animal
            {
                protected ILazyLoader? Loader { get; set; }
            }

            [LazyLoad]
            public partial class Dog : Animal
            {
                public partial Owner Owner { get; set; }
            }
            """);

        Assert.Single(LoaderMembersInHierarchy(result.Output, "Dog"));
        Assert.Contains("Load(Loader, this, ref field, nameof(Owner))", result.GeneratedSource("Dog.LazyLoad.g.cs"));
    }

    [Fact]
    public void BaseInOtherAssembly_WithGeneratedLoader_IsReused()
    {
        var baseResult = GeneratorHarness.RunClean($$"""
            using Rally25rs.EFCoreLazyLoad;
            namespace Zoo;
            {{RelatedTypes}}

            [LazyLoad]
            public partial class Animal
            {
                public partial Owner Owner { get; set; }
            }
            """, assemblyName: "BaseAssembly");

        var result = GeneratorHarness.RunClean("""
            using Rally25rs.EFCoreLazyLoad;
            namespace Zoo.Derived;

            [LazyLoad]
            public partial class Dog : global::Zoo.Animal
            {
                public partial global::Zoo.Toy Toy { get; set; }
            }
            """, extraReferences: [baseResult.Output.ToMetadataReference()], includeAttribute: false);

        Assert.Single(LoaderMembersInHierarchy(result.Output, "Zoo.Derived.Dog"));
        Assert.DoesNotContain("ILazyLoader", result.GeneratedSource("Zoo.Derived.Dog.LazyLoad.g.cs"));
    }

    private static List<ISymbol> LoaderMembersInHierarchy(Compilation compilation, string metadataName)
    {
        var members = new List<ISymbol>();
        for (var type = compilation.GetTypeByMetadataName(metadataName); type is not null; type = type.BaseType)
        {
            members.AddRange(type.GetMembers().Where(m => m switch
            {
                IFieldSymbol { IsImplicitlyDeclared: false } f => f.Type.Name == "ILazyLoader",
                IPropertySymbol p => p.Type.Name == "ILazyLoader",
                _ => false,
            }));
        }

        Assert.NotNull(compilation.GetTypeByMetadataName(metadataName));
        return members;
    }

    [Fact]
    public void AbstractBaseEntity_InProjectGeneratingXmlDocs_HasNoMissingDocWarning()
    {
        // The generated loader field is protected on a public type, so it needs a doc comment (CS1591), which
        // projects with <GenerateDocumentationFile> and warnings as errors would otherwise fail on.
        var compilation = GeneratorHarness.CreateCompilation(
            """
            using Rally25rs.EFCoreLazyLoad;
            namespace Zoo;

            /// <summary>A related entity.</summary>
            public class Owner
            {
                /// <summary>The key.</summary>
                public int Id { get; set; }
            }

            /// <summary>The base of every entity.</summary>
            [LazyLoad]
            public abstract partial class EntityBase
            {
                /// <summary>The key.</summary>
                public int Id { get; set; }
            }

            /// <summary>A pet.</summary>
            [LazyLoad]
            public partial class Pet : EntityBase
            {
                /// <summary>The owner.</summary>
                public partial Owner Owner { get; set; }
            }
            """,
            documentationMode: DocumentationMode.Diagnose);

        GeneratorHarness.CreateDriver(documentationMode: DocumentationMode.Diagnose)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(generatorDiagnostics);
        Assert.Contains(
            output.SyntaxTrees,
            t => t.ToString().Contains("protected global::Microsoft.EntityFrameworkCore.Infrastructure.ILazyLoader? __lazyLoader;", StringComparison.Ordinal));
        var docProblems = output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Id.StartsWith("CS15", StringComparison.Ordinal)
                && d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true)
            .ToList();
        Assert.True(docProblems.Count == 0, string.Join("\n", docProblems));
    }
}
