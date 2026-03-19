using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class OverlapPathTests
{
    [Fact]
    public void AllUnmanaged_Compiles()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
    }

    [Fact]
    public void KindEnum_HasVariants()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
                [DiscriminatedUnion]
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
    public void Factories_Exist()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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

        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape")!;

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
    public void Factories_HaveCorrectSignatures()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
    public void MixedManagedUnmanaged_Compiles()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
                partial struct Event
                {
                    [Variant] static partial void Click(int x, int y, string target);
                    [Variant] static partial void Hover(float posX, float posY);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var eventType = compilation.GetTypeByMetadataName("TestNs.Event");
        Assert.NotNull(eventType);

        // Factories exist
        var factories = eventType!.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.Name.StartsWith("New"))
            .Select(m => m.Name)
            .ToList();
        Assert.Contains("NewClick", factories);
        Assert.Contains("NewHover", factories);
    }

    [Fact]
    public void RefFieldsOverlap()
    {
        // Two variants with different ref types at the same Region 2 slot
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
                partial struct Message
                {
                    [Variant] static partial void Text(string content);
                    [Variant] static partial void Error(object detail);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var msgType = compilation.GetTypeByMetadataName("TestNs.Message")!;

        // Both factories exist
        var newText = msgType.GetMembers("NewText")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newText);
        Assert.Single(newText!.Parameters);

        var newError = msgType.GetMembers("NewError")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newError);
        Assert.Single(newError!.Parameters);
    }

    [Fact]
    public void Region3_BoxedField()
    {
        // A field that is neither unmanaged-with-known-size nor reference type -> Region 3 (Boxed)
        // Using a user-defined struct param (IsUnmanaged=true but KnownSize=null for user structs)
        var source = """
            using Spire;
            namespace TestNs
            {
                public struct Point { public int X; public int Y; }

                [DiscriminatedUnion]
                partial struct Drawing
                {
                    [Variant] static partial void Dot(Point location);
                    [Variant] static partial void Line(Point start, Point end);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var drawingType = compilation.GetTypeByMetadataName("TestNs.Drawing")!;

        var newDot = drawingType.GetMembers("NewDot")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newDot);

        var newLine = drawingType.GetMembers("NewLine")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.NotNull(newLine);
        Assert.Equal(2, newLine!.Parameters.Length);
    }

    [Fact]
    public void FieldlessVariant()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
    }

    [Fact]
    public void Deconstruct_SharedArity()
    {
        // Circle and Square both have 1 field -> shared arity -> object? Deconstruct
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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

        // Shared arity Deconstruct: (out Kind, out object?)
        var sharedDeconstruct = deconstructMethods
            .SingleOrDefault(m => m.Parameters.Length == 2);
        Assert.NotNull(sharedDeconstruct);
        Assert.Equal("Kind", sharedDeconstruct!.Parameters[0].Type.Name);
        Assert.Equal(SpecialType.System_Object,
            sharedDeconstruct.Parameters[1].Type.OriginalDefinition.SpecialType);
    }

    [Fact]
    public void Deconstruct_UniqueArity()
    {
        // Only Rectangle has 2 fields -> unique arity -> typed Deconstruct
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
    public void ReadonlyStruct()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
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
    public void NamespacedStruct()
    {
        var source = """
            using Spire;
            namespace My.Deep.Namespace
            {
                [DiscriminatedUnion]
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
    }
}
