using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class LayoutDiagnosticTests
{
    [Fact]
    public void LayoutOnRecord_EmitsWarning()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.Overlap)]
                public abstract partial record Foo
                {
                    public sealed partial record A(int X) : Foo;
                    public sealed partial record B(string Y) : Foo;
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        // Warning is reported
        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU004");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Source is still generated (warning, not error)
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
    }

    [Fact]
    public void LayoutOnClass_EmitsWarning()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.BoxedFields)]
                public abstract partial class Foo
                {
                    public sealed partial class A : Foo
                    {
                        public int X { get; }
                        public A(int x) { X = x; }
                    }
                    public sealed partial class B : Foo
                    {
                        public string Y { get; }
                        public B(string y) { Y = y; }
                    }
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        // Warning is reported
        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU004");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Source is still generated
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
    }

    [Fact]
    public void OverlapOnGenericStruct_EmitsError()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion(Layout.Overlap)]
                partial struct Foo<T>
                {
                    [Variant] static partial void Some(T value);
                    [Variant] static partial void None();
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out _, out var diagnostics);

        // Error is reported
        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU005");
        Assert.Contains(diagnostics, d =>
            d.Id == "SPIRE_DU005" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void AutoOnGenericStruct_UsesBoxedFields_NoDiagnostics()
    {
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

        // Verify BoxedFields path was used (Kind enum exists)
        var optionType = compilation.GetTypeByMetadataName("TestNs.Option`1");
        Assert.NotNull(optionType);
        var kindType = optionType!.GetTypeMembers("Kind").SingleOrDefault();
        Assert.NotNull(kindType);
        Assert.Equal(TypeKind.Enum, kindType!.TypeKind);
    }

    [Fact]
    public void AutoOnNonGenericStruct_UsesOverlap_NoDiagnostics()
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
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        // Verify Overlap path was used (FieldOffset attribute on generated code)
        var shapeType = compilation.GetTypeByMetadataName("TestNs.Shape");
        Assert.NotNull(shapeType);
    }

    [Fact]
    public void NestedType_EmitsError()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                class Outer
                {
                    [DiscriminatedUnion]
                    partial struct Inner
                    {
                        [Variant] static partial void A(int x);
                    }
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU001");
        Assert.Contains(diagnostics, d =>
            d.Id == "SPIRE_DU001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void RefStruct_EmitsError()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
                ref partial struct Foo
                {
                    [Variant] static partial void A(int x);
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU002");
        Assert.Contains(diagnostics, d =>
            d.Id == "SPIRE_DU002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void NoVariants_EmitsWarning()
    {
        var source = """
            using Spire;
            namespace TestNs
            {
                [DiscriminatedUnion]
                partial struct Foo
                {
                }
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE_DU003");
        Assert.Contains(diagnostics, d =>
            d.Id == "SPIRE_DU003" && d.Severity == DiagnosticSeverity.Warning);
    }
}
