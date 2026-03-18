using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class RecordPathTests
{
    [Fact]
    public void BasicGenericRecord_GeneratesAbstractRecordWithSealedVariants()
    {
        var source = """
            using Spire;

            namespace TestNs
            {
                [DiscriminatedUnion]
                public partial record Option<T>
                {
                    public partial record Some(T Value) : Option<T>;
                    public partial record None() : Option<T>;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var parentType = compilation.GetTypeByMetadataName("TestNs.Option`1");
        Assert.NotNull(parentType);
        Assert.True(parentType!.IsAbstract);

        // Verify sealed variant types exist
        var someType = parentType.GetTypeMembers("Some").SingleOrDefault();
        Assert.NotNull(someType);
        Assert.True(someType!.IsSealed);

        var noneType = parentType.GetTypeMembers("None").SingleOrDefault();
        Assert.NotNull(noneType);
        Assert.True(noneType!.IsSealed);
    }

    [Fact]
    public void NonGenericRecord_Compiles()
    {
        var source = """
            using Spire;

            namespace Shapes
            {
                [DiscriminatedUnion]
                public partial record Shape
                {
                    public partial record Circle(double Radius) : Shape;
                    public partial record Square(int Side) : Shape;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var shapeType = compilation.GetTypeByMetadataName("Shapes.Shape");
        Assert.NotNull(shapeType);
        Assert.True(shapeType!.IsAbstract);

        var circleType = shapeType.GetTypeMembers("Circle").SingleOrDefault();
        Assert.NotNull(circleType);
        Assert.True(circleType!.IsSealed);

        var squareType = shapeType.GetTypeMembers("Square").SingleOrDefault();
        Assert.NotNull(squareType);
        Assert.True(squareType!.IsSealed);
    }

    [Fact]
    public void FieldlessVariantOnly_Compiles()
    {
        var source = """
            using Spire;

            namespace Traffic
            {
                [DiscriminatedUnion]
                public partial record Light
                {
                    public partial record Red() : Light;
                    public partial record Yellow() : Light;
                    public partial record Green() : Light;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var lightType = compilation.GetTypeByMetadataName("Traffic.Light");
        Assert.NotNull(lightType);
        Assert.True(lightType!.IsAbstract);

        Assert.NotNull(lightType.GetTypeMembers("Red").SingleOrDefault());
        Assert.NotNull(lightType.GetTypeMembers("Yellow").SingleOrDefault());
        Assert.NotNull(lightType.GetTypeMembers("Green").SingleOrDefault());
    }

    [Fact]
    public void MultiFieldVariant_Compiles()
    {
        var source = """
            using Spire;

            namespace Geo
            {
                [DiscriminatedUnion]
                public partial record Shape
                {
                    public partial record Rectangle(float Width, float Height) : Shape;
                    public partial record Circle(double Radius) : Shape;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var shapeType = compilation.GetTypeByMetadataName("Geo.Shape");
        Assert.NotNull(shapeType);

        var rectType = shapeType!.GetTypeMembers("Rectangle").SingleOrDefault();
        Assert.NotNull(rectType);

        // Rectangle should have 2 properties (Width and Height from primary ctor)
        var rectProps = rectType!.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.Name == "Width" || p.Name == "Height")
            .Select(p => p.Name)
            .ToList();
        Assert.Contains("Width", rectProps);
        Assert.Contains("Height", rectProps);
    }

    [Fact]
    public void NamespacedRecord_HasCorrectNamespace()
    {
        var source = """
            using Spire;

            namespace My.Deep.Namespace
            {
                [DiscriminatedUnion]
                public partial record Result
                {
                    public partial record Ok() : Result;
                    public partial record Error() : Result;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var resultType = compilation.GetTypeByMetadataName("My.Deep.Namespace.Result");
        Assert.NotNull(resultType);
        Assert.Equal("My.Deep.Namespace",
            resultType!.ContainingNamespace.ToDisplayString());
    }

    [Fact]
    public void InternalRecord_HasCorrectAccessibility()
    {
        var source = """
            using Spire;

            namespace Access
            {
                [DiscriminatedUnion]
                internal partial record Outcome
                {
                    public partial record Success(int Code) : Outcome;
                    public partial record Failure(string Message) : Outcome;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var outcomeType = compilation.GetTypeByMetadataName("Access.Outcome");
        Assert.NotNull(outcomeType);
        Assert.Equal(Accessibility.Internal, outcomeType!.DeclaredAccessibility);
    }
}
