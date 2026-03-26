using Xunit;

namespace Spire.SourceGenerators.Tests;

public class AttributeInjectionTests
{
    [Fact]
    public void Attributes_AreInjected_AndCompile()
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

        // Verify the attribute types exist in compilation
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
