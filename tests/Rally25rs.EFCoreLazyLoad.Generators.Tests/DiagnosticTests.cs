using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rally25rs.EFCoreLazyLoad.Generators.Tests;

public class DiagnosticTests
{
    private const string Prelude = """
        using Rally25rs.EFCoreLazyLoad;
        public class Address { public int Id { get; set; } }
        """;

    [Fact]
    public void EFLL001_ClassNotPartial()
    {
        var diagnostic = AssertSingleDiagnostic("EFLL001", """
            [LazyLoad]
            public class Customer { public Address? Address { get; set; } }
            """);
        Assert.Contains("'Customer'", diagnostic.GetMessage());
    }

    [Fact]
    public void EFLL001_ContainingTypeNotPartial()
    {
        AssertSingleDiagnostic("EFLL001", """
            public class Outer
            {
                [LazyLoad]
                public partial class Customer { public partial Address Address { get; set; } }
            }
            """);
    }

    [Fact]
    public void EFLL001_PropertyAttributeInNonPartialClass()
    {
        AssertSingleDiagnostic("EFLL001", """
            public class Customer { [LazyLoad] public Address? Address { get; set; } }
            """);
    }

    [Fact]
    public void EFLL002_PropertyNotPartial()
    {
        var diagnostic = AssertSingleDiagnostic("EFLL002", """
            public partial class Customer { [LazyLoad] public Address? Address { get; set; } }
            """);
        Assert.Contains("Customer.Address", diagnostic.GetMessage());
        Assert.Equal("Entities.cs", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void EFLL003_PropertyAlreadyImplemented()
    {
        AssertSingleDiagnostic("EFLL003", """
            public partial class Customer
            {
                [LazyLoad] public partial Address? Address { get; set; }
                public partial Address? Address { get => field; set => field = value; }
            }
            """);
    }

    [Fact]
    public void EFLL004_ValueTypeProperty()
    {
        var diagnostic = AssertSingleDiagnostic("EFLL004", """
            public partial class Customer { [LazyLoad] public partial int Count { get; set; } }
            """);
        Assert.Contains("'int'", diagnostic.GetMessage());
    }

    [Fact]
    public void EFLL004_ValueTypeProperty_ClassLevel()
    {
        AssertSingleDiagnostic("EFLL004", """
            [LazyLoad]
            public partial class Customer { public partial System.DateTime When { get; set; } }
            """);
    }

    [Fact]
    public void EFLL005_NoGetter()
    {
        AssertSingleDiagnostic("EFLL005", """
            public partial class Customer { [LazyLoad] public partial Address Address { set; } }
            """);
    }

    [Fact]
    public void EFLL006_StaticProperty()
    {
        AssertSingleDiagnostic("EFLL006", """
            public partial class Customer { [LazyLoad] public static partial Address Address { get; set; } }
            """);
    }

    [Fact]
    public void EFLL007_InaccessibleInheritedLoader()
    {
        var baseCompilation = GeneratorHarness.CreateCompilation("""
            using Microsoft.EntityFrameworkCore.Infrastructure;
            public class Animal { private ILazyLoader? _loader; public object? L => _loader; }
            """, assemblyName: "BaseAssembly", includeAttribute: false);

        var result = GeneratorHarness.Run(
            Prelude + """
            [LazyLoad]
            public partial class Dog : Animal { public partial Address Address { get; set; } }
            """,
            extraReferences: [baseCompilation.ToMetadataReference()]);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("EFLL007", diagnostic.Id);
        Assert.Contains("Animal._loader", diagnostic.GetMessage());
    }

    [Fact]
    public void EFLL008_LanguageVersionTooLow()
    {
        var result = GeneratorHarness.Run(
            Prelude + """
            [LazyLoad]
            public partial class Customer { public partial Address Address { get; set; } }
            """,
            LanguageVersion.CSharp13);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("EFLL008", diagnostic.Id);
        Assert.Empty(result.DriverResult.GeneratedTrees);
    }

    [Fact]
    public void ValidPropertiesStillGenerated_WhenSiblingHasDiagnostic()
    {
        var result = GeneratorHarness.Run(Prelude + """
            [LazyLoad]
            public partial class Customer
            {
                public partial int Count { get; set; }
                public partial Address Address { get; set; }
            }
            """);

        Assert.Equal("EFLL004", Assert.Single(result.GeneratorDiagnostics).Id);
        Assert.Contains("nameof(Address)", result.GeneratedSource("Customer.LazyLoad.g.cs"));
    }

    private static Diagnostic AssertSingleDiagnostic(string id, string source)
    {
        var result = GeneratorHarness.Run(Prelude + source);
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        return diagnostic;
    }
}
