using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

public sealed class NamespaceAliasTests
{
    [Fact]
    public void NamedNamespace_GlobalVariantsType_DoesNotBlockConversionExecution()
    {
        var result = Generate(NamedNamespaceSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var consumer = assembly.GetType("Named.Consumer", throwOnError: true)!;
        var value = consumer.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        var radius = consumer.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new[] { value });
        Assert.Equal(7, radius);
    }

    [Fact]
    public void NamedNamespace_NestedUnionWithGlobalAliasNames_CompilesAndReadsFields()
    {
        var result = Generate(NestedNamedNamespaceSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var consumer = assembly.GetType("Nested.NamedConsumer", throwOnError: true)!;
        var value = consumer.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        var count = consumer.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new[] { value });
        Assert.Equal(11, count);
    }

    [Fact]
    public void NamedNamespace_CSharp9PlacesAliasesInsideNamespaceDeclaration()
    {
        var result = Generate(NamedNamespaceSource(), LanguageVersion.CSharp9);
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);

        var tree = FindUnionTree(result.RunResult, "Shape");
        var root = (CompilationUnitSyntax)tree.GetRoot();
        Assert.Empty(root.Usings);

        var namespaceDeclaration = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .Single(namespaceNode => namespaceNode.Name.ToString() == "Named");
        Assert.NotEmpty(namespaceDeclaration.Usings);
        Assert.DoesNotContain(namespaceDeclaration.Usings, usingDirective =>
            usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword));
    }

    [Fact]
    public void GlobalNamespace_RootAliasFallbackRemainsAtCompilationUnit()
    {
        var result = Generate(GlobalNamespaceSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);

        var tree = FindUnionTree(result.RunResult, "RootShape");
        var root = (CompilationUnitSyntax)tree.GetRoot();
        Assert.NotEmpty(root.Usings);
        Assert.DoesNotContain(root.Usings, usingDirective =>
            usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword));
        Assert.Empty(root.Members.OfType<NamespaceDeclarationSyntax>());
    }

    [Fact]
    public void SameNamespaceAliasCollision_RemainsCompilerOwned()
    {
        var result = Generate(SameNamespaceCollisionSource());
        AssertNoGeneratorDiagnostics(result);

        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, diagnostic => diagnostic.Id == "CS0576");
    }

    private static GeneratedResult Generate(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "namespace-aliases.cs");
        var compilation = CSharpCompilation.Create(
            $"NamespaceAliases_{Guid.NewGuid():N}",
            new[] { tree },
            UnityGeneratorTestHarness.GetReferences(false, false),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
                new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        return new GeneratedResult(outputCompilation, diagnostics, driver.GetRunResult());
    }

    private static SyntaxTree FindUnionTree(
        GeneratorDriverRunResult result,
        string unionName)
        => result.GeneratedTrees.Single(tree =>
            tree.GetRoot().DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(declaration => declaration.Identifier.ValueText == unionName));

    private static void AssertNoGeneratorDiagnostics(GeneratedResult result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(diagnostics.Length == 0, string.Join("\n", diagnostics));
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics,
        GeneratorDriverRunResult RunResult);

    private static string NamedNamespaceSource() => """
        using Houtamelo.Spire;
        public sealed class Variants { }
        namespace Named
        {
            [DiscriminatedUnion]
            public partial struct Shape
            {
                [Variant] public static partial Shape Circle(int radius);
            }
            public static class Consumer
            {
                public static Shape Create() => new ShapeTypes.Variants.Circle(7);
                public static int Read(Shape value) => value.circle.radius;
            }
        }
        """;

    private static string NestedNamedNamespaceSource() => """
        using Houtamelo.Spire;
        public sealed class Fields { }
        public sealed class Serializable { }
        public sealed class StructLayout { }
        public sealed class LayoutKind { }
        public sealed class EditorBrowsable { }
        public sealed class EditorBrowsableState { }
        namespace Nested
        {
            public static partial class Outer
            {
                public static partial class Inner
                {
                    [DiscriminatedUnion]
                    public partial struct Command
                    {
                        [Variant] public static partial Command Run(int count);
                    }
                }
            }
            public static class NamedConsumer
            {
                public static Outer.Inner.Command Create()
                    => new Outer.Inner.CommandTypes.Variants.Run(11);
                public static int Read(Outer.Inner.Command value)
                    => value.run.count;
            }
        }
        """;

    private static string GlobalNamespaceSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct RootShape
        {
            [Variant] public static partial RootShape Value(int value);
        }
        public static class RootConsumer
        {
            public static RootShape Create()
                => new RootShapeTypes.Variants.Value(13);
        }
        """;

    private static string SameNamespaceCollisionSource() => """
        using Houtamelo.Spire;
        namespace Collision
        {
            public sealed class Serializable { }
            [DiscriminatedUnion]
            public partial struct Shape
            {
                [Variant] public static partial Shape Value(int value);
            }
        }
        """;
}
