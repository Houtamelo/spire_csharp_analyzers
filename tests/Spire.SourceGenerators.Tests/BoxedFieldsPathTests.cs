using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class BoxedFieldsPathTests
{
    [Fact]
    public void BasicBoxedFields_Compiles()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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
                [DiscriminatedUnion(Layout.BoxedFields)]
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
    public void TagField_IsPublicReadonly()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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

        // Verify the tag field exists, is public, and is readonly
        var tagField = shapeType.GetMembers("tag")
            .OfType<IFieldSymbol>()
            .SingleOrDefault();

        Assert.NotNull(tagField);
        Assert.True(tagField!.IsReadOnly);
        Assert.Equal(Accessibility.Public, tagField.DeclaredAccessibility);
        Assert.Equal("Kind", tagField.Type.Name);
    }

    [Fact]
    public void Factories_HaveCorrectSignatures()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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
    public void Deconstruct_SharedArity()
    {
        // Circle and Square both have 1 field -> shared arity -> object? Deconstruct
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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

        // Shared arity Deconstruct: (out Kind, out object?)
        var deconstructMethods = shapeType.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>()
            .ToList();

        var sharedDeconstruct = deconstructMethods
            .SingleOrDefault(m => m.Parameters.Length == 2);
        Assert.NotNull(sharedDeconstruct);
        Assert.Equal("Kind", sharedDeconstruct!.Parameters[0].Type.Name);
        Assert.Equal(SpecialType.System_Object, sharedDeconstruct.Parameters[1].Type
            .OriginalDefinition.SpecialType);
    }

    [Fact]
    public void Deconstruct_UniqueArity()
    {
        // Only Rectangle has 2 fields -> unique arity -> typed Deconstruct
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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

        // Unique arity: (out Kind, out float, out float)
        var typedDeconstruct = deconstructMethods
            .SingleOrDefault(m => m.Parameters.Length == 3);
        Assert.NotNull(typedDeconstruct);
        Assert.Equal("Kind", typedDeconstruct!.Parameters[0].Type.Name);
        Assert.Equal(SpecialType.System_Single, typedDeconstruct.Parameters[1].Type.SpecialType);
        Assert.Equal(SpecialType.System_Single, typedDeconstruct.Parameters[2].Type.SpecialType);
    }

    [Fact]
    public void GenericStruct_FallsBackToBoxedFields()
    {
        // Auto layout on generic struct -> BoxedFields
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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

        // Should have Kind enum (BoxedFields path), not nested types (record path)
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
    public void FieldlessVariant_HandledCorrectly()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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
    public void AllFieldlessVariants_GeneratesDeconstructWithObjectParam()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
                partial struct Light
                {
                    [Variant] static partial void Red();
                    [Variant] static partial void Yellow();
                    [Variant] static partial void Green();
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var lightType = compilation.GetTypeByMetadataName("TestNs.Light")!;

        // Should have a Deconstruct(out Kind, out object?) even though all variants are fieldless
        var deconstructMethods = lightType.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>()
            .ToList();

        Assert.Single(deconstructMethods);
        var d = deconstructMethods[0];
        Assert.Equal(2, d.Parameters.Length);
        Assert.Equal("Kind", d.Parameters[0].Type.Name);
    }

    [Fact]
    public void ReadonlyStruct_EmitsReadonlyModifier()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
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
    public void InternalStruct_UsesInternalAccessibility()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
                internal partial struct Internal
                {
                    [Variant] static partial void X(int v);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var unionType = compilation.GetTypeByMetadataName("TestNs.Internal")!;
        Assert.Equal(Accessibility.Internal, unionType.DeclaredAccessibility);
    }
}
