using System;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Generator diagnostics for the declaration boundary of record-struct variants.
public sealed class RecordVariantDeclarationDiagnosticTests
{
    [Fact]
    public void NonPublicRecordVariant_IsRejectedWithTheRecordVariantDiagnostic()
    {
        var diagnostics = Run("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                private record struct Hidden(int value);
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE_DU019");
    }

    [Fact]
    public void IndependentlyGenericRecordVariant_IsRejectedWithTheRecordVariantDiagnostic()
    {
        var diagnostics = Run("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                public record struct Generic<T>(T value);
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE_DU019");
    }

    [Fact]
    public void RecordClassAndOrdinaryStructVariants_AreRejected()
    {
        var diagnostics = Run("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public record ClassVariant(int value);
                [Variant] public struct StructVariant { public int value; }
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == "SPIRE_DU019") >= 2,
            string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.GetMessage())));
    }

    [Fact]
    public void FieldlessRecordVariants_AreValid()
    {
        var diagnostics = Run("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public record struct None;
                [Variant] public record struct Empty();
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id.StartsWith("SPIRE_DU", StringComparison.Ordinal));
    }

    [Fact]
    public void ContainingUnionTypeParameters_AreValidOnRecordVariant()
    {
        var diagnostics = Run("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization<T>
            {
                [Variant] public record struct Some(T value);
                [Variant] public static partial Customization<T> Empty();
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id.StartsWith("SPIRE_DU", StringComparison.Ordinal));
    }

    private static System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Run(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out _,
            out var diagnostics,
            path: "record-variant-case.cs");
        return diagnostics;
    }
}
