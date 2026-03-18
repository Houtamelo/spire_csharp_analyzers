using Xunit;

namespace Spire.SourceGenerators.Tests;

public class AttributeInjectionTests
{
    [Fact]
    public void Attributes_AreInjected_AndCompile()
    {
        var source = """
            using Spire;

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
            "Spire.DiscriminatedUnionAttribute");
        Assert.NotNull(duAttr);

        var variantAttr = compilation.GetTypeByMetadataName(
            "Spire.VariantAttribute");
        Assert.NotNull(variantAttr);

        var layoutEnum = compilation.GetTypeByMetadataName("Spire.Layout");
        Assert.NotNull(layoutEnum);
    }
}
