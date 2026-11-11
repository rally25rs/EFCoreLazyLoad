using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rally25rs.EFCoreLazyLoad.Generators.Tests;

public class IncrementalTests
{
    private const string Source = """
        using Rally25rs.EFCoreLazyLoad;
        public class Address { public int Id { get; set; } }

        [LazyLoad]
        public partial class Customer { public partial Address Address { get; set; } }

        public partial class Order { [LazyLoad] public partial Address ShipTo { get; set; } }
        """;

    [Fact]
    public void UnrelatedEdit_ReusesCachedOutputs()
    {
        var compilation = GeneratorHarness.CreateCompilation(Source);
        var driver = GeneratorHarness.CreateDriver().RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal(2, driver.GetRunResult().GeneratedTrees.Length);

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "public class Unrelated { public int Value { get; set; } }",
            GeneratorHarness.ParseOptions,
            path: "Unrelated.cs",
            cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult().Results.Single();

        var typeModelOutputs = result.TrackedSteps[LazyLoadGenerator.TrackingNames.TypeModels]
            .SelectMany(step => step.Outputs)
            .ToList();
        Assert.Equal(2, typeModelOutputs.Count);
        Assert.All(typeModelOutputs, o => Assert.Contains(o.Reason, new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged }));

        var sourceOutputs = result.TrackedOutputSteps.SelectMany(kv => kv.Value).SelectMany(step => step.Outputs).ToList();
        Assert.NotEmpty(sourceOutputs);
        Assert.All(sourceOutputs, o => Assert.Equal(IncrementalStepRunReason.Cached, o.Reason));
    }

    [Fact]
    public void EntityEdit_RegeneratesOnlyThatEntity()
    {
        var compilation = GeneratorHarness.CreateCompilation(Source);
        var driver = GeneratorHarness.CreateDriver().RunGenerators(compilation, TestContext.Current.CancellationToken);

        var extra = CSharpSyntaxTree.ParseText(
            "public partial class Customer { public partial Address Second { get; set; } }",
            GeneratorHarness.ParseOptions,
            path: "Customer2.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        driver = driver.RunGenerators(compilation.AddSyntaxTrees(extra), TestContext.Current.CancellationToken);
        var result = driver.GetRunResult().Results.Single();

        var reasons = result.TrackedOutputSteps.SelectMany(kv => kv.Value).SelectMany(step => step.Outputs)
            .Select(o => o.Reason)
            .ToList();
        Assert.Contains(IncrementalStepRunReason.Modified, reasons);
        Assert.Contains(IncrementalStepRunReason.Cached, reasons);
        Assert.Contains(
            "nameof(Second)",
            result.GeneratedSources.Single(s => s.HintName == "Customer.LazyLoad.g.cs").SourceText.ToString());
    }
}
