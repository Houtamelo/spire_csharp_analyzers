using System;
using System.Linq;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.RecordStructUnions;

[Trait("Environment", "Unity")]
public sealed class RecordStructUnionUnityTests
{
    [Fact]
    public void SerializableRecordStructUnion_ReportsExactDiagnosticAndSuppressesGeneration()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial record struct InvalidState
            {
                [Variant] public record struct Value(int amount);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        var diagnostic = Assert.Single(
            result.GeneratorDiagnostics,
            item => item.Id == "SPIRE_DU020");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Struct-based discriminated unions cannot be records. Remove the 'record' keyword.",
            diagnostic.GetMessage());
        Assert.NotNull(diagnostic.Location.SourceTree);
        Assert.Equal(
            "record",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Empty(result.RunResult.GeneratedTrees);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
    }

    [Fact]
    public void InvalidRecordStructUnion_DoesNotPreventValidStructUnionGeneration()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial record struct InvalidState
            {
                [Variant] public record struct Value(int amount);
            }

            [Serializable, DiscriminatedUnion]
            public partial struct ValidState
            {
                [Variant] public static partial ValidState Value(int amount);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        Assert.Single(result.GeneratorDiagnostics, item => item.Id == "SPIRE_DU020");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var generated = result.RunResult.GeneratedTrees
            .Select(tree => tree.GetText().ToString())
            .ToArray();
        Assert.Contains(generated, text => text.Contains("ValidState", StringComparison.Ordinal));
        Assert.DoesNotContain(generated, text => text.Contains("InvalidState", StringComparison.Ordinal));
        Assert.Contains(
            result.RunResult.GeneratedTrees,
            tree => tree.FilePath.EndsWith(
                "Spire.UnitySerializationSupport.generated.cs",
                StringComparison.Ordinal));

        var validUnion = result.OutputCompilation.GetTypeByMetadataName("ValidState");
        Assert.NotNull(validUnion);
        Assert.NotEmpty(validUnion!.GetMembers("Value"));
        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "A valid struct union must remain usable beside the rejected record struct union");
        Assert.NotNull(assembly.GetType("ValidState"));
    }

    [Fact]
    public void ReadonlyGenericRecordStructUnion_UsesDU020BeforeOtherShapeDiagnostics()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public readonly partial record struct InvalidState<T>
                where T : unmanaged
            {
                [Variant] public record struct Value(T amount);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        var diagnostic = Assert.Single(
            result.GeneratorDiagnostics,
            item => item.Id == "SPIRE_DU020");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain(result.GeneratorDiagnostics, item => item.Id == "SPIRE_DU018");
        Assert.Empty(result.RunResult.GeneratedTrees);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
    }
}
