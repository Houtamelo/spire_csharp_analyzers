using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class BoxedTuplePathTests
{
    [Fact]
    public void BasicBoxedTuple_Compiles()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                    [Variant] static partial void Square(int sideLength);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape");
        Assert.NotNull(shapeType);
        Assert.True(shapeType!.IsValueType);

        // Kind enum exists
        var kindType = shapeType.GetTypeMembers("Kind").SingleOrDefault();
        Assert.NotNull(kindType);
        Assert.Equal(TypeKind.Enum, kindType!.TypeKind);

        // Factories exist
        var factories = shapeType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.Name.StartsWith("New"))
            .Select(m => m.Name)
            .ToList();
        Assert.Contains("NewCircle", factories);
        Assert.Contains("NewRectangle", factories);
        Assert.Contains("NewSquare", factories);
    }

    [Fact]
    public void KindEnum_HasAllVariants()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                    [Variant] static partial void Square(int sideLength);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out _);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;
        var kindType = shapeType.GetTypeMembers("Kind").Single();

        var members = kindType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name)
            .ToList();

        Assert.Contains("Circle", members);
        Assert.Contains("Rectangle", members);
        Assert.Contains("Square", members);
        Assert.Equal(3, members.Count);
    }

    [Fact]
    public void TagField_Exists()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out _);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;

        var tagField = shapeType.GetMembers("tag")
            .OfType<IFieldSymbol>()
            .SingleOrDefault();

        Assert.NotNull(tagField);
        Assert.True(tagField!.IsReadOnly);
        Assert.Equal(Accessibility.Public, tagField.DeclaredAccessibility);
        Assert.Equal("Kind", tagField.Type.Name);
    }

    [Fact]
    public void SinglePayloadField()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                    [Variant] static partial void Square(int sideLength);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out _);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;

        // Should have exactly 2 fields: tag + _payload
        var fields = shapeType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared)
            .ToList();

        Assert.Equal(2, fields.Count);

        var payloadField = fields.SingleOrDefault(f => f.Name == "_payload");
        Assert.NotNull(payloadField);
        Assert.Equal(SpecialType.System_Object, payloadField!.Type.OriginalDefinition.SpecialType);
        Assert.False(payloadField.IsReadOnly is false && payloadField.DeclaredAccessibility == Accessibility.Public,
            "_payload should not be public and mutable");
    }

    [Fact]
    public void Factories_HaveCorrectSignatures()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                    [Variant] static partial void Square(int sideLength);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out _);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;

        var newCircle = shapeType.GetMembers("NewCircle")
            .OfType<IMethodSymbol>().Single();
        Assert.Single(newCircle.Parameters);
        Assert.Equal(SpecialType.System_Double, newCircle.Parameters[0].Type.SpecialType);

        var newRect = shapeType.GetMembers("NewRectangle")
            .OfType<IMethodSymbol>().Single();
        Assert.Equal(2, newRect.Parameters.Length);
        Assert.Equal(SpecialType.System_Single, newRect.Parameters[0].Type.SpecialType);
        Assert.Equal(SpecialType.System_Single, newRect.Parameters[1].Type.SpecialType);

        var newSquare = shapeType.GetMembers("NewSquare")
            .OfType<IMethodSymbol>().Single();
        Assert.Single(newSquare.Parameters);
        Assert.Equal(SpecialType.System_Int32, newSquare.Parameters[0].Type.SpecialType);
    }

    [Fact]
    public void Deconstruct_SingleOverload()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Shape
                {
                    [Variant] static partial void Circle(double radius);
                    [Variant] static partial void Rectangle(float width, float height);
                    [Variant] static partial void Square(int sideLength);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out _);

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;

        var deconstructMethods = shapeType.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>()
            .ToList();

        // BoxedTuple always emits exactly one Deconstruct: (out Kind, out object?)
        Assert.Single(deconstructMethods);
        var d = deconstructMethods[0];
        Assert.Equal(2, d.Parameters.Length);
        Assert.Equal("Kind", d.Parameters[0].Type.Name);
        Assert.Equal(SpecialType.System_Object, d.Parameters[1].Type.OriginalDefinition.SpecialType);
    }

    [Fact]
    public void FieldlessVariant()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Token
                {
                    [Variant] static partial void Ident(string name);
                    [Variant] static partial void Number(int value);
                    [Variant] static partial void Eof();
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var tokenType = compilation.GetTypeByMetadataName("TestNs.Token")!;

        // Eof factory exists with no parameters
        var newEof = tokenType.GetMembers("NewEof")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newEof);
        Assert.Empty(newEof!.Parameters);

        // Kind enum has Eof
        var kindType = tokenType.GetTypeMembers("Kind").Single();
        var members = kindType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name)
            .ToList();
        Assert.Contains("Eof", members);
    }

    [Fact]
    public void ReadonlyStruct()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                readonly partial struct Immutable
                {
                    [Variant] static partial void A(int x);
                    [Variant] static partial void B(string y);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var immType = compilation.GetTypeByMetadataName("TestNs.Immutable")!;
        Assert.True(immType.IsReadOnly);
    }

    [Fact]
    public void GenericStruct()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Option<T>
                {
                    [Variant] static partial void Some(T value);
                    [Variant] static partial void None();
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var optionType = compilation.GetTypeByMetadataName("TestNs.Option`1");
        Assert.NotNull(optionType);

        // Kind enum exists
        var kindType = optionType!.GetTypeMembers("Kind").SingleOrDefault();
        Assert.NotNull(kindType);
        Assert.Equal(TypeKind.Enum, kindType!.TypeKind);

        // Factories exist
        var newSome = optionType.GetMembers("NewSome")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newSome);
        Assert.Single(newSome!.Parameters);

        var newNone = optionType.GetMembers("NewNone")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newNone);
        Assert.Empty(newNone!.Parameters);
    }

    [Fact]
    public void NamespacedStruct()
    {
        var source = """
            using Spire;
            namespace My.Deep.Namespace
            {
                [DiscriminatedUnion(Layout.BoxedTuple)]
                partial struct Result
                {
                    [Variant] static partial void Ok(int value);
                    [Variant] static partial void Err(string message);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var resultType = compilation.GetTypeByMetadataName("My.Deep.Namespace.Result");
        Assert.NotNull(resultType);
        Assert.True(resultType!.IsValueType);

        // Kind enum exists
        var kindType = resultType.GetTypeMembers("Kind").SingleOrDefault();
        Assert.NotNull(kindType);
    }
}
