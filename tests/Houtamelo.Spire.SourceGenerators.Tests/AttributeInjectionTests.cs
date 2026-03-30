using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests;

public class AttributeResolutionTests
{
    [Fact]
    public void Attributes_ResolvedFromCoreAssembly_AndCompile()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Dummy
            {
                [Variant] static partial void A(int x);
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

        var duAttr = compilation.GetTypeByMetadataName(
            "Houtamelo.Spire.DiscriminatedUnionAttribute");
        Assert.NotNull(duAttr);

        var variantAttr = compilation.GetTypeByMetadataName(
            "Houtamelo.Spire.VariantAttribute");
        Assert.NotNull(variantAttr);

        var layoutEnum = compilation.GetTypeByMetadataName("Houtamelo.Spire.Layout");
        Assert.NotNull(layoutEnum);
    }
}
