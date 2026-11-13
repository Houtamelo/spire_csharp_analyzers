using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Regression coverage for record-variant declaration warnings that depend on
/// authored syntax rather than generated partial declarations.
public sealed class RecordVariantWarningEdgeTests
{
    [Fact]
    public async Task AuthoredPositionalFieldOverride_ReportsReconstructionWarning()
    {
        var diagnostics = await Analyze(FieldOverrideSource);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");
    }

    [Fact]
    public async Task NestedUnrelatedProperty_DoesNotReportReconstructionWarning()
    {
        var diagnostics = await Analyze(UnrelatedNestedPropertySource);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");
    }

    [Fact]
    public async Task PositionalParametersFromLaterRecordPartialPart_AreAnalyzed()
    {
        var diagnostics = await Analyze(LaterPartialParameterSource);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");
    }

    [Fact]
    public async Task ExplicitPositionalFieldSerializationAttributes_SuppressMissingFieldWarning()
    {
        var diagnostics = await Analyze(ExplicitFieldAttributesSource);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task NonPartialSerializationWarning_ExplainsPartialRemedy()
    {
        var diagnostics = await Analyze(NonPartialMissingAttributesSource);
        var warning = Assert.Single(diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE028"));

        Assert.Contains("partial", warning.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(string source)
    {
        var result = UnityGeneratorTestHarness.Run(source, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var diagnostics = await result.OutputCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics.Where(diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE029")
            .ToImmutableArray();
    }

    private const string FieldOverrideSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct FieldOverrideUnion
        {
            [Variant]
            public record struct Value(int value)
            {
                public int value;
            }
        }
        """;

    private const string UnrelatedNestedPropertySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct UnrelatedPropertyUnion
        {
            [Variant]
            public record struct Value(int value)
            {
                public struct Helper
                {
                    public int value { get; set; }
                }
            }
        }
        """;

    private const string LaterPartialParameterSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct LaterPartialUnion
        {
            [Variant] public partial record struct Split;
        }

        public partial struct LaterPartialUnion
        {
            public partial record struct Split(int value)
            {
                public int value { get; init; } = value + 1;
            }
        }
        """;

    private const string ExplicitFieldAttributesSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct ExplicitFieldUnion
        {
            [Variant]
            [Serializable]
            public record struct Value(
                [field: SerializeField] int inline,
                [field: SerializeReference] object? reference);
        }
        """;

    private const string NonPartialMissingAttributesSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct NonPartialMissingUnion
        {
            [Variant] public record struct Value(int value);
        }
        """;
}
