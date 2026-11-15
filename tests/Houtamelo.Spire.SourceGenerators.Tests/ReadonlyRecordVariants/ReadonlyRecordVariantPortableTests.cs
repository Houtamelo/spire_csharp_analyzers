using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.SourceGenerators.Tests.Behavioral;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadonlyRecordVariants;

/// CoreCLR regressions for readonly record variants. Unity-only serialization
/// diagnostics must not leak into portable compilations.
public sealed class ReadonlyRecordVariantPortableTests : BehavioralTestBase
{
    [Fact]
    public async Task SerializableReadonlyPartialVariant_OutsideUnityHasNoSerializationDiagnostics()
    {
        var diagnostics = await Analyze(SerializableReadonlyPartialSource);

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is "SPIRE028" or "SPIRE030");
    }

    [Fact]
    public async Task SerializableReadonlyNonPartialVariant_OutsideUnityHasNoSerializationDiagnostics()
    {
        var diagnostics = await Analyze(SerializableReadonlyNonPartialSource);

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is "SPIRE028" or "SPIRE030");
    }

    [Fact]
    public void ReadonlyVariantStillConvertsReconstructsAndComparesOutsideUnity()
    {
        var assembly = CompileAndLoad(RuntimeSource);
        var union = GetType(assembly, "PortableState");
        var payloadType = GetType(assembly, "PortableState+Value");
        var payload = Activator.CreateInstance(payloadType, 42)!;
        var secondPayload = Activator.CreateInstance(payloadType, 42)!;

        var conversion = union.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "op_Implicit" &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == payloadType);
        var value = conversion.Invoke(null, new[] { payload })!;
        var secondValue = conversion.Invoke(null, new[] { secondPayload })!;

        var reconstructed = union.GetProperty("value")!.GetValue(value)!;
        Assert.Equal(payloadType, reconstructed.GetType());
        Assert.Equal(42, payloadType.GetProperty("amount")!.GetValue(reconstructed));
        Assert.True(value.Equals(secondValue));
        Assert.Equal(value.GetHashCode(), secondValue.GetHashCode());
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "readonly-record-portable.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var diagnostics = await outputCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE029" or "SPIRE030")
            .ToImmutableArray();
    }

    private const string SerializableReadonlyPartialSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct PortableState
        {
            [Variant] public readonly partial record struct Value(int amount);
        }
        """;

    private const string SerializableReadonlyNonPartialSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct PortableState
        {
            [Variant] public readonly record struct Value(int amount);
        }
        """;

    private const string RuntimeSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct PortableState
        {
            [Variant] public readonly record struct Value(int amount);
        }
        """;
}
