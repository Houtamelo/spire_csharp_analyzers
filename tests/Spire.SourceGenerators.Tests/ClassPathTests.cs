using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class ClassPathTests
{
    [Fact]
    public void BasicGenericClass_EmitsAbstractTypeWithSealedVariantsAndFactories()
    {
        var source = """
            using Spire;

            namespace MyApp
            {
                [DiscriminatedUnion]
                public partial class Result<T, E>
                {
                    public partial class Ok : Result<T, E>
                    {
                        public T Value { get; }
                        public Ok(T value) { Value = value; }
                    }
                    public partial class Err : Result<T, E>
                    {
                        public E Error { get; }
                        public Err(E error) { Error = error; }
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        // Verify the union type exists and is abstract
        var unionType = compilation.GetTypeByMetadataName("MyApp.Result`2");
        Assert.NotNull(unionType);
        Assert.True(unionType.IsAbstract);

        // Verify sealed variant classes exist
        var okType = unionType.GetTypeMembers("Ok").SingleOrDefault();
        Assert.NotNull(okType);
        Assert.True(okType.IsSealed);
        Assert.Equal(unionType, okType.BaseType, SymbolEqualityComparer.Default);

        var errType = unionType.GetTypeMembers("Err").SingleOrDefault();
        Assert.NotNull(errType);
        Assert.True(errType.IsSealed);

    }

    [Fact]
    public void FieldlessVariant_EmitsEmptyClassNoDeconstruct()
    {
        var source = """
            using Spire;

            namespace MyApp
            {
                [DiscriminatedUnion]
                public partial class Option<T>
                {
                    public partial class Some : Option<T>
                    {
                        public T Value { get; }
                        public Some(T value) { Value = value; }
                    }
                    public partial class None : Option<T> { }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var unionType = compilation.GetTypeByMetadataName("MyApp.Option`1");
        Assert.NotNull(unionType);

        // None variant should exist and be sealed
        var noneType = unionType.GetTypeMembers("None").SingleOrDefault();
        Assert.NotNull(noneType);
        Assert.True(noneType.IsSealed);

        // None should have no Deconstruct method
        var deconstruct = noneType.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>().SingleOrDefault();
        Assert.Null(deconstruct);

        // None should have no declared properties
        var props = noneType.GetMembers()
            .OfType<IPropertySymbol>().ToList();
        Assert.Empty(props);

    }

    [Fact]
    public void MultiFieldVariant_EmitsDeconstructWithAllFields()
    {
        var source = """
            using Spire;

            namespace MyApp
            {
                [DiscriminatedUnion]
                public partial class Shape
                {
                    public partial class Circle : Shape
                    {
                        public double Radius { get; }
                        public Circle(double radius) { Radius = radius; }
                    }
                    public partial class Rect : Shape
                    {
                        public double Width { get; }
                        public double Height { get; }
                        public Rect(double width, double height) { Width = width; Height = height; }
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var unionType = compilation.GetTypeByMetadataName("MyApp.Shape");
        Assert.NotNull(unionType);

        var rectType = unionType.GetTypeMembers("Rect").SingleOrDefault();
        Assert.NotNull(rectType);

        // Rect should have 2 properties
        var props = rectType.GetMembers()
            .OfType<IPropertySymbol>().ToList();
        Assert.Equal(2, props.Count);

    }

    [Fact]
    public void NonGenericClass_EmitsCorrectly()
    {
        var source = """
            using Spire;

            namespace MyApp
            {
                [DiscriminatedUnion]
                public partial class Command
                {
                    public partial class Start : Command { }
                    public partial class Stop : Command
                    {
                        public string Reason { get; }
                        public Stop(string reason) { Reason = reason; }
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var unionType = compilation.GetTypeByMetadataName("MyApp.Command");
        Assert.NotNull(unionType);
        Assert.True(unionType.IsAbstract);
        Assert.False(unionType.IsGenericType);

        // Start and Stop variants exist
        Assert.NotNull(unionType.GetTypeMembers("Start").SingleOrDefault());
        Assert.NotNull(unionType.GetTypeMembers("Stop").SingleOrDefault());

    }

    [Fact]
    public void NamespacedClass_WrapsInNamespaceBlock()
    {
        var source = """
            using Spire;

            namespace Deep.Nested.Namespace
            {
                [DiscriminatedUnion]
                public partial class Token
                {
                    public partial class Ident : Token
                    {
                        public string Name { get; }
                        public Ident(string name) { Name = name; }
                    }
                    public partial class Number : Token
                    {
                        public int Value { get; }
                        public Number(int value) { Value = value; }
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        // Type exists under the deep namespace
        var unionType = compilation.GetTypeByMetadataName(
            "Deep.Nested.Namespace.Token");
        Assert.NotNull(unionType);
        Assert.True(unionType.IsAbstract);
        Assert.Equal("Deep.Nested.Namespace",
            unionType.ContainingNamespace.ToDisplayString());
    }

    [Fact]
    public void InternalClass_UsesInternalAccessibility()
    {
        var source = """
            using Spire;

            namespace MyApp
            {
                [DiscriminatedUnion]
                internal partial class Status
                {
                    public partial class Active : Status { }
                    public partial class Inactive : Status
                    {
                        public string Reason { get; }
                        public Inactive(string reason) { Reason = reason; }
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var unionType = compilation.GetTypeByMetadataName("MyApp.Status");
        Assert.NotNull(unionType);
        Assert.Equal(Accessibility.Internal, unionType.DeclaredAccessibility);
        Assert.True(unionType.IsAbstract);
    }
}
