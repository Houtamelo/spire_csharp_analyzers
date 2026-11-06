using System.IO;
using System.Linq;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Diagnostic;

public sealed class NoVariantsDiagnosticMessageTests
{
    [Fact]
    public void EmptyUnion_UsesFunctionsDiagnosticText()
    {
        GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct EmptyUnion { }
            """,
            out var compilation,
            out var diagnostics,
            path: "empty-union.cs");

        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var diagnostic = Assert.Single(
            diagnostics.Where(item => item.Id == "SPIRE_DU003"));
        Assert.Equal("SPIRE_DU003", diagnostic.Id);
        Assert.Equal("No [Variant] functions found", diagnostic.Descriptor.Title.ToString());
        Assert.Equal(
            "No [Variant] functions found on discriminated union type",
            diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal("empty-union.cs", diagnostic.Location.SourceTree?.FilePath);
    }

    [Fact]
    public void UnionWithVariant_HasNoNoVariantsDiagnosticAndEmits()
    {
        GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Value
            {
                [Variant] public static partial Value Create(int value);
            }
            """,
            out var compilation,
            out var diagnostics,
            path: "valid-union.cs");

        Assert.Empty(diagnostics.Where(item => item.Id == "SPIRE_DU003"));
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join("\n", emit.Diagnostics.Select(item =>
                $"{item.Id}: {item.GetMessage()}")));
    }
}
