using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadonlyRecordVariants;

[Trait("Environment", "Unity")]
public sealed class ReadonlyRecordVariantsTests
{
    [Fact]
    public async Task ReadonlyPartialVariant_ReportsWarningAtReadonlyTokenAndEmits()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly partial record struct Slider(int amount);
            }
            """;

        var (result, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "Slider", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
        _ = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "A readonly record variant must remain a valid runtime payload");
    }

    [Fact]
    public async Task ReadonlyNonPartialVariant_ReportsWarningInsteadOfMissingAttributeWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "Slider", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task ReadonlyFullyAnnotatedVariant_StillReportsReadonlyWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                [Serializable]
                public readonly partial record struct Slider([field: SerializeField] int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "Slider", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task ReadonlyManagedReferenceVariant_StillReportsReadonlyWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            public sealed class Node { }

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                public readonly partial record struct NodeValue([field: SerializeReference] Node node);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "NodeValue", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task MultipleReadonlyVariants_ReportOneWarningPerVariant()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly partial record struct Slider(int sliderAmount);
                [Variant] public readonly partial record struct Color(int colorValue);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warnings = diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE030").ToArray();

        Assert.Equal(2, warnings.Length);
        AssertReadonlyWarning(warnings.Single(diagnostic => diagnostic.GetMessage().Contains("Slider", StringComparison.Ordinal)), source, "Slider", "Customization");
        AssertReadonlyWarning(warnings.Single(diagnostic => diagnostic.GetMessage().Contains("Color", StringComparison.Ordinal)), source, "Color", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task GenericOuterUnionReadonlyVariant_ReportsWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization<T>
                where T : unmanaged
            {
                [Variant] public readonly record struct Value(T payload);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "Value", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task MixedVariants_ReportOnlyReadonlyVariant()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                [Serializable]
                public readonly partial record struct ImmutableValue([field: SerializeField] int immutableAmount);

                [Variant]
                [Serializable]
                public partial record struct MutableValue([field: SerializeField] int mutableAmount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");

        AssertReadonlyWarning(warning, source, "ImmutableValue", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE030" && diagnostic.GetMessage().Contains("MutableValue", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task ReadonlyVariantWithAuthoredPositionalMember_ReportsBothWarnings()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                [Serializable]
                public readonly partial record struct Slider([field: SerializeField] int amount)
                {
                    public int amount { get; init; } = amount + 1;
                }
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        var readonlyWarning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
        var reconstructionWarning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");

        AssertReadonlyWarning(readonlyWarning, source, "Slider", "Customization");
        Assert.Contains("amount", reconstructionWarning.GetMessage(), StringComparison.Ordinal);
        Assert.NotEqual(readonlyWarning.Location.SourceSpan, reconstructionWarning.Location.SourceSpan);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task ReadonlyModifierOnLaterPartialPart_ReportsOneWarningOnAuthoredReadonlyToken()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public partial record struct Slider(int amount);
            }

            public partial struct Customization
            {
                public readonly partial record struct Slider;
            }
            """;

        var (result, diagnostics) = await AnalyzeUnityAllowingGeneratorDiagnostics(source);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var warning = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
        AssertReadonlyWarning(warning, source, "Slider", "Customization");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task FieldlessReadonlyVariantWithoutParameterList_IsNotWarned()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly record struct Empty;
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task FieldlessReadonlyVariantWithEmptyParameterList_IsNotWarned()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly record struct Empty();
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task ReadonlyOuterUnion_IsRejectedWithoutNestedReadonlyWarning()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public readonly partial struct Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (result, diagnostics) = await AnalyzeUnityAllowingGeneratorDiagnostics(source);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task RefOuterUnion_IsRejectedWithoutNestedReadonlyWarning()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public ref partial struct Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (result, diagnostics) = await AnalyzeUnityAllowingGeneratorDiagnostics(source);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task RecordStructOuterUnion_IsRejectedWithoutNestedReadonlyWarning()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial record struct Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (result, diagnostics) = await AnalyzeUnityAllowingGeneratorDiagnostics(source);
        var diagnostic = Assert.Single(
            result.GeneratorDiagnostics,
            item => item.Id == "SPIRE_DU020");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "record",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(),
            item => item.Id is "CS0261" or "CS0556" or "CS0563" or "CS0738");
        Assert.Empty(result.RunResult.GeneratedTrees);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task FieldlessReadonlyAndFieldedMutableVariants_KeepExistingFieldlessWarningOnly()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly record struct Empty;
                [Variant] [Serializable] public partial record struct Value([field: SerializeField] int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task NonSerializableUnion_DoesNotReportUnityReadonlyWarning()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE030");
    }

    [Fact]
    public async Task MutableVariant_DoesNotReportReadonlyWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                [Serializable]
                public partial record struct Slider([field: SerializeField] int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task ReadonlyNestedNonVariantRecord_DoesNotReportWarning()
    {
        const string source = """
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion]
            public partial struct Customization
            {
                [Variant]
                [Serializable]
                public partial record struct Slider([field: SerializeField] int amount);

                public readonly record struct Metadata(int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task SemanticAttributeLookalikes_DontTriggerReadonlyWarning()
    {
        const string source = """
            using System;

            namespace Lookalike
            {
                [AttributeUsage(AttributeTargets.Struct)]
                public sealed class DiscriminatedUnionAttribute : Attribute { }

                [AttributeUsage(AttributeTargets.Struct)]
                public sealed class VariantAttribute : Attribute { }
            }

            [Serializable, Lookalike.DiscriminatedUnion]
            public partial struct Customization
            {
                [Lookalike.Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (_, diagnostics) = await AnalyzeUnity(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    [Fact]
    public async Task UnsupportedClassUnion_DoesNotCascadeReadonlyWarning()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial class Customization
            {
                [Variant] public readonly record struct Slider(int amount);
            }
            """;

        var (result, diagnostics) = await AnalyzeUnityAllowingGeneratorDiagnostics(source);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU011");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE030");
    }

    private static async Task<(UnityGeneratorRun Result, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics)> AnalyzeUnity(
        string source)
    {
        var result = UnityGeneratorTestHarness.Run(source, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return (result, await AnalyzeOutput(result.OutputCompilation));
    }

    private static async Task<(UnityGeneratorRun Result, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics)> AnalyzeUnityAllowingGeneratorDiagnostics(
        string source)
    {
        var result = UnityGeneratorTestHarness.Run(source, includeUnityReferences: true);
        return (result, await AnalyzeOutput(result.OutputCompilation));
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeOutput(Compilation compilation)
    {
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE029" or "SPIRE030")
            .ToImmutableArray();
    }

    private static void AssertReadonlyWarning(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string source,
        string variantName,
        string unionName)
    {
        Assert.Equal("SPIRE030", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains(variantName, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(unionName, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Remove 'readonly'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.NotNull(diagnostic.Location.SourceTree);
        Assert.Equal(
            "readonly",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        var syntaxRoot = CSharpSyntaxTree.ParseText(source).GetRoot();
        var authoredReadonlyStarts = syntaxRoot.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Where(record => record.Identifier.ValueText == variantName)
            .SelectMany(record => record.Modifiers
                .Where(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
                .Select(modifier => modifier.SpanStart))
            .ToArray();
        Assert.NotEmpty(authoredReadonlyStarts);
        Assert.Contains(diagnostic.Location.SourceSpan.Start, authoredReadonlyStarts);
    }
}
