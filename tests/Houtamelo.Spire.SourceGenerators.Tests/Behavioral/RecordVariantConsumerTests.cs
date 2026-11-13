using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Consumer integration tests for authored record-struct variants. The source
/// under test is compiled through the generator, then checked through the same
/// analyzers and code fix that consumers use.
public sealed class RecordVariantConsumerTests : BehavioralTestBase
{
    [Fact]
    public void RecordOnlyUnion_SupportsPropertyAndPositionalPatterns()
    {
        var assembly = CompileAndLoad(RecordOnlySource);
        var union = assembly.GetType("RecordChoice", throwOnError: true)!;
        var consumer = assembly.GetType("RecordChoiceConsumer", throwOnError: true)!;

        var slider = Activator.CreateInstance(
            union.GetNestedType("Slider")!,
            new object[] { 7 })!;
        var empty = Activator.CreateInstance(union.GetNestedType("Empty")!)!;
        var sliderValue = union.GetMethod("FromSlider")!.Invoke(null, new[] { slider })!;
        var emptyValue = union.GetMethod("FromEmpty")!.Invoke(null, new[] { empty })!;

        Assert.Equal(7, consumer.GetMethod("MatchPositional")!.Invoke(null, new[] { sliderValue }));
        Assert.Equal(14, consumer.GetMethod("ReadComputedMember")!.Invoke(null, new[] { sliderValue }));
        Assert.Equal(1, consumer.GetMethod("MatchProperty")!.Invoke(null, new[] { sliderValue }));
        Assert.Equal(0, consumer.GetMethod("MatchProperty")!.Invoke(null, new[] { emptyValue }));
    }

    [Fact]
    public void MixedUnion_UsesAuthoredAndGeneratedPayloadsInPatterns()
    {
        var assembly = CompileAndLoad(MixedSource);
        var union = assembly.GetType("MixedChoice", throwOnError: true)!;
        var consumer = assembly.GetType("MixedChoiceConsumer", throwOnError: true)!;

        var slider = Activator.CreateInstance(union.GetNestedType("Slider")!, new object[] { 9 })!;
        var color = union.GetMethod("Color")!.Invoke(null, new object[] { 3, 4 })!;
        var sliderValue = union.GetMethod("FromSlider")!.Invoke(null, new[] { slider })!;

        Assert.Equal(9, consumer.GetMethod("Match")!.Invoke(null, new[] { sliderValue }));
        Assert.Equal(7, consumer.GetMethod("Match")!.Invoke(null, new[] { color }));
    }

    [Fact]
    public void GenericRecordUnion_UsesContainingTypeArgumentsInPatterns()
    {
        var assembly = CompileAndLoad(GenericSource);
        var openUnion = assembly.GetType("GenericChoice`1", throwOnError: true)!;
        var union = openUnion.MakeGenericType(typeof(int));
        var consumer = assembly.GetType("GenericChoiceConsumer", throwOnError: true)!;

        var someDefinition = openUnion.GetNestedType("Some")!;
        var someType = someDefinition.IsGenericTypeDefinition
            ? someDefinition.MakeGenericType(typeof(int))
            : union.GetNestedType("Some")!;
        var some = Activator.CreateInstance(someType, new object[] { 12 })!;
        var value = union.GetMethod("FromSome")!.Invoke(null, new[] { some })!;

        Assert.Equal(12, consumer.GetMethod("Read")!.MakeGenericMethod(typeof(int)).Invoke(null, new[] { value }));
    }

    [Fact]
    public async Task GuardAnalyzer_AcceptsStableRecordAccessorAndReportsUnsafeReads()
    {
        var output = Generate(GuardSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is "SPIRE013" or "SPIRE014" &&
            DiagnosticLineContains(diagnostic, GuardSource, "value.IsSlider ?"));
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(diagnostic, GuardSource, "value.slider.value;"));
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE013" &&
            DiagnosticLineContains(diagnostic, GuardSource, "value.IsEmpty ?"));
    }

    [Fact]
    public async Task ExhaustivenessAnalyzer_EnumeratesRecordAndFieldlessVariants()
    {
        var output = Generate(ExhaustivenessSource, out _);
        var diagnostics = await Analyze(output, new ExhaustivenessAnalyzer());
        var spire = diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE009").ToArray();

        Assert.Contains(spire, diagnostic =>
            DiagnosticLineContains(diagnostic, ExhaustivenessSource, "public static int Incomplete") &&
            diagnostic.GetMessage().Contains("Empty", StringComparison.Ordinal));
        Assert.DoesNotContain(spire, diagnostic =>
            DiagnosticLineContains(diagnostic, ExhaustivenessSource, "public static int Complete"));
        Assert.DoesNotContain(spire, diagnostic =>
            DiagnosticLineContains(diagnostic, ExhaustivenessSource, "public static int NullableComplete"));
    }

    [Fact]
    public async Task TypeSafetyAnalyzer_UsesRecordPositionalFieldSchema()
    {
        var output = Generate(TypeSafetySource, out _);
        var diagnostics = await Analyze(output, new TypeSafetyAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE011");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE012");
    }

    [Fact]
    public async Task GuardAnalyzer_RecordGetterSideEffectInvalidatesSharedReceiver()
    {
        var output = Generate(RecordGetterSideEffectSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                RecordGetterSideEffectSource,
                "_ = GetterHolder.Value.a.value;"));
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                RecordGetterSideEffectSource,
                "_ = GetterHolder.Value.a;"));
    }

    [Fact]
    public void RecordGetterSideEffect_ChangesSharedUnionAtRuntime()
    {
        var assembly = CompileAndLoad(RecordGetterSideEffectSource);
        var consumer = assembly.GetType("GetterConsumer", throwOnError: true)!;

        Assert.True((bool)consumer.GetMethod("RuntimeKindChanges")!.Invoke(null, null)!);
    }

    [Fact]
    public async Task GuardAnalyzer_SynthesizedRecordDeconstructReadsImpureGetter()
    {
        var output = Generate(SynthesizedDeconstructGetterSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                SynthesizedDeconstructGetterSource,
                "_ = SynthesizedHolder.Value.a;"));
    }

    [Fact]
    public void SynthesizedRecordDeconstruct_ChangesSharedUnionAtRuntime()
    {
        var assembly = CompileAndLoad(SynthesizedDeconstructGetterSource);
        var consumer = assembly.GetType("SynthesizedConsumer", throwOnError: true)!;

        Assert.True((bool)consumer.GetMethod("RuntimeKindChanges")!.Invoke(null, null)!);
    }

    [Fact]
    public async Task GuardAnalyzer_RecordDeconstructSideEffectInvalidatesSharedReceiver()
    {
        var output = Generate(RecordDeconstructSideEffectSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                RecordDeconstructSideEffectSource,
                "_ = DeconstructHolder.Value.a;"));
    }

    [Fact]
    public void RecordDeconstructSideEffect_ChangesSharedUnionAtRuntime()
    {
        var assembly = CompileAndLoad(RecordDeconstructSideEffectSource);
        var consumer = assembly.GetType("DeconstructConsumer", throwOnError: true)!;

        Assert.True((bool)consumer.GetMethod("RuntimeKindChanges")!.Invoke(null, null)!);
    }

    [Fact]
    public async Task GuardAnalyzer_RecordPayloadInitializerInvalidatesSharedReceiver()
    {
        var output = Generate(RecordPayloadInitializerSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                RecordPayloadInitializerSource,
                "_ = InitializerHolder.Value.a;"));
    }

    [Fact]
    public void RecordPayloadInitializer_ChangesSharedUnionAtRuntime()
    {
        var assembly = CompileAndLoad(RecordPayloadInitializerSource);
        var consumer = assembly.GetType("InitializerConsumer", throwOnError: true)!;

        Assert.True((bool)consumer.GetMethod("RuntimeKindChanges")!.Invoke(null, null)!);
    }

    [Fact]
    public async Task GuardAnalyzer_FieldlessRecordConstructorInvalidatesSharedReceiver()
    {
        var output = Generate(FieldlessConstructorSideEffectSource, out _);
        var diagnostics = await Analyze(output, new FieldAccessSafetyAnalyzer());

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "SPIRE014" &&
            DiagnosticLineContains(
                diagnostic,
                FieldlessConstructorSideEffectSource,
                "_ = ConstructorHolder.Value.empty;"));
    }

    [Fact]
    public void FieldlessRecordConstructor_ChangesSharedUnionAtRuntime()
    {
        var assembly = CompileAndLoad(FieldlessConstructorSideEffectSource);
        var consumer = assembly.GetType("ConstructorConsumer", throwOnError: true)!;

        Assert.True((bool)consumer.GetMethod("RuntimeKindChanges")!.Invoke(null, null)!);
    }

    [Fact]
    public async Task AddMissingArmsCodeFix_UsesAuthoredRecordDeconstruct()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct FixChoice
            {
                [Variant] public record struct Slider(int value);
                [Variant] public record struct Empty;
            }

            public static class FixConsumer
            {
                public static int Read(FixChoice value) => value switch
                {
                    { IsEmpty: true } => 0,
                };
            }
            """;

        var fixedSource = await ApplyMissingArmsFix(source);
        Assert.Contains("IsSlider", fixedSource, StringComparison.Ordinal);
        Assert.Matches(
            @"slider\s*:\s*var\s*\(\s*value\d+\s*\)",
            fixedSource);

        var fixedOutput = Generate(fixedSource, out var generatorDiagnostics);
        AssertNoWarnings(generatorDiagnostics);
        AssertNoErrors(fixedOutput);
        var remaining = await Analyze(fixedOutput, new ExhaustivenessAnalyzer());
        Assert.DoesNotContain(remaining, diagnostic => diagnostic.Id == "SPIRE009");
    }

    [Fact]
    public async Task AddMissingArmsCodeFix_UsesPositionalPartWhenVariantIsSplit()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct SplitChoice
            {
                [Variant] public partial record struct Item;
                public partial record struct Item(int value)
                {
                    public Item(int value, int ignored) : this(value) { }

                    public void Deconstruct(out int value, out int ignored)
                    {
                        value = this.value;
                        ignored = 99;
                    }
                }

                [Variant] public record struct Empty;
            }

            public static class SplitConsumer
            {
                public static int Read(SplitChoice value) => value switch
                {
                    { IsEmpty: true } => 0,
                };
            }
            """;

        var fixedSource = await ApplyMissingArmsFix(source);
        Assert.Contains("IsItem", fixedSource, StringComparison.Ordinal);
        Assert.Matches(
            @"item\s*:\s*var\s*\(\s*value\d*\s*\)",
            fixedSource);
        Assert.DoesNotMatch(
            @"item\s*:\s*var\s*\(\s*value\d+\s*,",
            fixedSource);

        var fixedOutput = Generate(fixedSource, out var generatorDiagnostics);
        AssertNoWarnings(generatorDiagnostics);
        AssertNoErrors(fixedOutput);
        var remaining = await Analyze(fixedOutput, new ExhaustivenessAnalyzer());
        Assert.DoesNotContain(remaining, diagnostic => diagnostic.Id == "SPIRE009");
    }

    private static Compilation Generate(
        string source,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> generatorDiagnostics)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out generatorDiagnostics,
            path: "record-consumer.cs");
        return outputCompilation;
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(
        Compilation compilation,
        params DiagnosticAnalyzer[] analyzers)
    {
        return await compilation.WithAnalyzers(analyzers.ToImmutableArray())
            .GetAnalyzerDiagnosticsAsync();
    }

    private static void AssertNoWarnings(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Severity >= DiagnosticSeverity.Warning);
    }

    private static void AssertNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine,
            errors.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static async Task<string> ApplyMissingArmsFix(string source)
    {
        var output = Generate(source, out _);
        var diagnostics = await Analyze(output, new ExhaustivenessAnalyzer());
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE009");

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RecordVariantConsumer", "RecordVariantConsumer", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)output.SyntaxTrees.First().Options)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)output.Options);
        foreach (var reference in output.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? sourceDocument = null;
        foreach (var tree in output.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, tree.FilePath, tree.GetText());
            if (tree.FilePath.EndsWith("record-consumer.cs", StringComparison.Ordinal))
                sourceDocument = documentId;
        }

        Assert.NotNull(sourceDocument);
        var document = solution.GetDocument(sourceDocument!)!;
        var actions = new List<CodeAction>();
        await new AddMissingArmsCodeFix().RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changedDocument = apply.ChangedSolution.GetDocument(sourceDocument!)!;
        return (await changedDocument.GetTextAsync()).ToString();
    }

    private static bool DiagnosticLineContains(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string source,
        string fragment)
    {
        var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
        var lines = source.Split('\n');
        return line >= 0 && line < lines.Length &&
            lines[line].Contains(fragment, StringComparison.Ordinal);
    }

    private const string RecordOnlySource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct RecordChoice
        {
            [Variant] public record struct Slider(int value)
            {
                public int DoubleValue => value * 2;
            }

            [Variant] public record struct Empty;

            public static RecordChoice FromSlider(Slider value) => value;
            public static RecordChoice FromEmpty(Empty value) => value;
        }

        public static class RecordChoiceConsumer
        {
            public static int MatchPositional(RecordChoice value) => value switch
            {
                { IsSlider: true, slider: var (number) } => number,
                { IsEmpty: true } => 0,
            };

            public static int ReadComputedMember(RecordChoice value)
                => value.slider.DoubleValue;

            public static int MatchProperty(RecordChoice value) => value switch
            {
                { IsSlider: true, slider: { value: > 0 } } => 1,
                _ => 0,
            };
        }
        """;

    private const string MixedSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct MixedChoice
        {
            [Variant] public record struct Slider(int value);
            [Variant] public static partial MixedChoice Color(int red, int blue);

            public static MixedChoice FromSlider(Slider value) => value;
        }

        public static class MixedChoiceConsumer
        {
            public static int Match(MixedChoice value) => value switch
            {
                { IsSlider: true, slider: var (number) } => number,
                { IsColor: true, color: var (red, blue) } => red + blue,
            };
        }
        """;

    private const string GenericSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct GenericChoice<T>
        {
            [Variant] public record struct Some(T value);
            [Variant] public record struct Empty;

            public static GenericChoice<T> FromSome(Some value) => value;
        }

        public static class GenericChoiceConsumer
        {
            public static int Read<T>(GenericChoice<T> value) => value switch
            {
                { IsSome: true, some: var (item) } => item is null ? 0 : 12,
                { IsEmpty: true } => -1,
            };
        }
        """;

    private const string GuardSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct GuardChoice
        {
            [Variant] public record struct Slider(int value);
            [Variant] public record struct Empty;
        }

        public static class GuardChoiceConsumer
        {
            public static int Good(GuardChoice value)
                => value.IsSlider ? value.slider.value : 0;

            public static int Bad(GuardChoice value)
                => value.slider.value;

            public static int Wrong(GuardChoice value)
                => value.IsEmpty ? value.slider.value : 0;
        }
        """;

    private const string ExhaustivenessSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct ExhaustiveChoice
        {
            [Variant] public record struct Slider(int value);
            [Variant] public record struct Empty;
        }

        public static class ExhaustiveChoiceConsumer
        {
            public static int Incomplete(ExhaustiveChoice value) => value switch
            {
                { IsSlider: true, slider: var (number) } => number,
            };

            public static int Complete(ExhaustiveChoice value) => value switch
            {
                { IsSlider: true, slider: var (number) } => number,
                { IsEmpty: true } => 0,
            };

            public static int NullableComplete(ExhaustiveChoice? value) => value switch
            {
                null => -1,
                { IsSlider: true, slider: var (number) } => number,
                { IsEmpty: true } => 0,
            };
        }
        """;

    private const string TypeSafetySource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct TypeChoice
        {
            [Variant] public record struct Slider(int value);
            [Variant] public record struct Empty;
        }

        public static class TypeChoiceConsumer
        {
            public static int WrongType(TypeChoice value) => value switch
            {
                { IsSlider: true, slider: { value: string bad } } => 1,
                { IsEmpty: true } => 0,
            };

            public static int WrongCount(TypeChoice value) => value switch
            {
                { IsSlider: true, slider: (int first, int second) } => first + second,
                { IsEmpty: true } => 0,
            };
        }
        """;

    private const string RecordGetterSideEffectSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct GetterChoice
        {
            [Variant] public record struct A(int value)
            {
                public int value => MutateSharedUnion();

                private static int MutateSharedUnion()
                {
                    GetterHolder.Value = new GetterChoice.Other();
                    return 7;
                }
            }

            [Variant] public record struct Other;
        }

        public static class GetterHolder
        {
            public static GetterChoice Value = new GetterChoice.Other();
        }

        public static class GetterConsumer
        {
            public static int Read()
            {
                if (!GetterHolder.Value.IsA)
                    return 0;

                _ = GetterHolder.Value.a.value;
                _ = GetterHolder.Value.a;
                return 1;
            }

            public static bool RuntimeKindChanges()
            {
                GetterHolder.Value = new GetterChoice.A(4);
                if (!GetterHolder.Value.IsA)
                    return false;

                _ = GetterHolder.Value.a.value;
                return GetterHolder.Value.IsOther;
            }
        }
        """;

    private const string RecordDeconstructSideEffectSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct DeconstructChoice
        {
            [Variant] public record struct A(int value)
            {
                public void Deconstruct(out int first, out int second)
                {
                    first = value;
                    second = 99;
                    DeconstructHolder.Value = new DeconstructChoice.Other();
                }
            }

            [Variant] public record struct Other;
        }

        public static class DeconstructHolder
        {
            public static DeconstructChoice Value = new DeconstructChoice.Other();
        }

        public static class DeconstructConsumer
        {
            public static int Read()
            {
                if (!DeconstructHolder.Value.IsA)
                    return 0;

                if (DeconstructHolder.Value.a is (var first, var second))
                    _ = DeconstructHolder.Value.a;
                return 1;
            }

            public static bool RuntimeKindChanges()
            {
                DeconstructHolder.Value = new DeconstructChoice.A(4);
                if (!DeconstructHolder.Value.IsA)
                    return false;

                if (DeconstructHolder.Value.a is (var first, var second))
                    return DeconstructHolder.Value.IsOther;
                return false;
            }
        }
        """;

    private const string SynthesizedDeconstructGetterSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct SynthesizedChoice
        {
            [Variant] public record struct A(int first, int second)
            {
                public int first => MutateSharedUnion();

                private static int MutateSharedUnion()
                {
                    SynthesizedHolder.Value = new SynthesizedChoice.Other();
                    return 7;
                }
            }

            [Variant] public record struct Other;
        }

        public static class SynthesizedHolder
        {
            public static SynthesizedChoice Value = new SynthesizedChoice.Other();
        }

        public static class SynthesizedConsumer
        {
            public static int Read()
            {
                if (!SynthesizedHolder.Value.IsA)
                    return 0;

                if (SynthesizedHolder.Value.a is (var first, var second))
                    _ = SynthesizedHolder.Value.a;
                return 1;
            }

            public static bool RuntimeKindChanges()
            {
                SynthesizedHolder.Value = new SynthesizedChoice.A(4, 5);
                if (!SynthesizedHolder.Value.IsA)
                    return false;

                if (SynthesizedHolder.Value.a is (var first, var second))
                    return SynthesizedHolder.Value.IsOther;
                return false;
            }
        }
        """;

    private const string RecordPayloadInitializerSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct InitializerChoice
        {
            [Variant] public record struct A(int value)
            {
                public int Extra { get; } = MutateSharedUnion();

                private static int MutateSharedUnion()
                {
                    InitializerHolder.Value = new InitializerChoice.Other();
                    return 99;
                }
            }

            [Variant] public record struct Other;
        }

        public static class InitializerHolder
        {
            public static InitializerChoice Value = new InitializerChoice.Other();
        }

        public static class InitializerConsumer
        {
            public static int Read()
            {
                if (!InitializerHolder.Value.IsA)
                    return 0;

                _ = InitializerHolder.Value.a;
                _ = InitializerHolder.Value.a;
                return 1;
            }

            public static bool RuntimeKindChanges()
            {
                InitializerHolder.Value = new InitializerChoice.A(4);
                if (!InitializerHolder.Value.IsA)
                    return false;

                _ = InitializerHolder.Value.a;
                return InitializerHolder.Value.IsOther;
            }
        }
        """;

    private const string FieldlessConstructorSideEffectSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct ConstructorChoice
        {
            [Variant] public record struct Empty
            {
                public Empty()
                {
                    ConstructorHolder.Value = new ConstructorChoice.Other();
                }
            }

            [Variant] public record struct Other;
        }

        public static class ConstructorHolder
        {
            public static ConstructorChoice Value = new ConstructorChoice.Other();
        }

        public static class ConstructorConsumer
        {
            public static int Read()
            {
                if (!ConstructorHolder.Value.IsEmpty)
                    return 0;

                _ = ConstructorHolder.Value.empty;
                _ = ConstructorHolder.Value.empty;
                return 1;
            }

            public static bool RuntimeKindChanges()
            {
                ConstructorHolder.Value = new ConstructorChoice.Empty();
                if (!ConstructorHolder.Value.IsEmpty)
                    return false;

                _ = ConstructorHolder.Value.empty;
                return ConstructorHolder.Value.IsOther;
            }
        }
        """;
}
