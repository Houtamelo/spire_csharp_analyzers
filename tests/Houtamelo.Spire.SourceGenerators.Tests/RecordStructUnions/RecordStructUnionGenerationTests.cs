using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.RecordStructUnions;

public sealed class RecordStructUnionGenerationTests
{
    [Fact]
    public void DiagnosticHasErrorSeverityExactMessageAndRecordSpan()
    {
        var result = Run(BasicSource, out _, out var diagnostics);
        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE_DU020"));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Struct-based discriminated unions cannot be records. Remove the 'record' keyword.",
            diagnostic.GetMessage());
        Assert.Equal(
            "record",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public void RejectedUnionDoesNotEmitImplementationOrCompilerCascade()
    {
        var result = Run(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial record struct BrokenUnion
            {
                [Variant] public record struct Value(int amount);
            }
            """,
            out var runResult,
            out var diagnostics);

        Assert.Contains(diagnostics, item => item.Id == "SPIRE_DU020");
        var generated = string.Join("\n", runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        Assert.DoesNotContain("BrokenUnion", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.GetDiagnostics(),
            item => item.Id is "CS0261" or "CS0556" or "CS0563" or "CS0738");
        GeneratorTestHelper.AssertNoCompilationErrors(result);
    }

    [Fact]
    public void ValidSiblingUnionStillGeneratesWhenAnotherUnionIsRejected()
    {
        var result = Run(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial record struct BrokenUnion
            {
                [Variant] public record struct Value(int amount);
            }

            [DiscriminatedUnion]
            public partial struct WorkingUnion
            {
                [Variant] public static partial WorkingUnion Value(int amount);
            }
            """,
            out var runResult,
            out var diagnostics);

        Assert.Contains(diagnostics, item => item.Id == "SPIRE_DU020");
        var generated = string.Join("\n", runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        Assert.Contains("WorkingUnion", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("BrokenUnion", generated, StringComparison.Ordinal);
        GeneratorTestHelper.AssertNoCompilationErrors(result);

        using var stream = new MemoryStream();
        var emit = result.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join("\n", emit.Diagnostics.Select(item => $"{item.Id}: {item.GetMessage()}")));
    }

    [Fact]
    public void JsonConfigurationDoesNotGenerateConvertersForRejectedUnion()
    {
        var result = Run(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion(JsonLibrary.SystemTextJson)]
            public partial record struct JsonUnion
            {
                [Variant] public record struct Value(int amount);
            }
            """,
            out var runResult,
            out var diagnostics);

        Assert.Contains(diagnostics, item => item.Id == "SPIRE_DU020");
        Assert.DoesNotContain(runResult.GeneratedTrees, tree =>
            tree.FilePath.EndsWith(".Stj.g.cs", StringComparison.Ordinal) ||
            tree.FilePath.EndsWith(".Nsj.g.cs", StringComparison.Ordinal) ||
            tree.FilePath.EndsWith(".Schema.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void AttributeOnPlainPartialPartStillRejectsRecordUnion()
    {
        var result = Run(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct SplitUnion
            {
                [Variant] public static partial SplitUnion Value(int amount);
            }

            public partial record struct SplitUnion
            {
            }
            """,
            out var runResult,
            out var diagnostics);

        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE_DU020"));
        Assert.Equal(
            "record",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        var generated = string.Join("\n", runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        Assert.DoesNotContain("SplitUnion", generated, StringComparison.Ordinal);
        // The authored partial declarations intentionally produce CS0261 until
        // the code fix removes `record`; this test only checks generator output.
    }

    private static Compilation Run(
        string source,
        out GeneratorDriverRunResult runResult,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        runResult = GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out diagnostics,
            path: "record-struct-union.cs");
        return outputCompilation;
    }

    private const string BasicSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct BrokenUnion
        {
            [Variant] public record struct Value(int amount);
        }
        """;
}
