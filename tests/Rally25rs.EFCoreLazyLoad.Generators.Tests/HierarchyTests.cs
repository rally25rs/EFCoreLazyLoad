using Microsoft.CodeAnalysis;

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
}
