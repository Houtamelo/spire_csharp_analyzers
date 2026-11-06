using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

/// Real generator/analyzer/suppressor checks for typed positional payloads.
public sealed class TypedPayloadIntegrationTests
{
    private const string MissingSibling = """
        #nullable enable
        using Houtamelo.Spire;

        public readonly struct Point { }

        [DiscriminatedUnion]
        public partial struct State
        {
            [Variant] public static partial State Idle();
            [Variant] public static partial State Moving(Point destination, object? marker);
            [Variant] public static partial State Speaking(string text, float timer);
        }

        public static class Consumer
        {
            public static int Read(State state) => state switch
            {
                { IsIdle: true } => 0,
                { IsMoving: true, moving: (Point destination, _) } => 1,
            };
        }
        """;

    private const string Complete = """
        #nullable enable
        using Houtamelo.Spire;

        public readonly struct Point { }

        [DiscriminatedUnion]
        public partial struct State
        {
            [Variant] public static partial State Idle();
            [Variant] public static partial State Moving(Point destination, object? marker);
            [Variant] public static partial State Speaking(string text, float timer);
        }

        public static class Consumer
        {
            public static int Read(State state) => state switch
            {
                { IsIdle: true } => 0,
                { IsMoving: true, moving: (Point destination, _) } => 1,
                { IsSpeaking: true, speaking: var (text, timer) } => 2,
            };
        }
        """;

    private const string NullableReferenceCapture = """
        #nullable enable
        using Houtamelo.Spire;

        public readonly struct Point { }

        [DiscriminatedUnion]
        public partial struct State
        {
            [Variant] public static partial State Idle();
            [Variant] public static partial State Moving(Point destination, object? marker);
            [Variant] public static partial State Speaking(string? text, float timer);
        }

        public static class Consumer
        {
            public static int Read(State state) => state switch
            {
                { IsIdle: true } => 0,
                { IsMoving: true, moving: (Point destination, _) } => 1,
                { IsSpeaking: true, speaking: (string text, var timer) } => 2,
            };
        }
        """;

    private const string TypedSecondValuePayloadMissingSibling = """
        #nullable enable
        using Houtamelo.Spire;

        public readonly struct Point { }

        [DiscriminatedUnion]
        public partial struct State
        {
            [Variant] public static partial State Idle();
            [Variant] public static partial State Moving(Point destination, float speed);
            [Variant] public static partial State Speaking(string text, float timer);
        }

        public static class Consumer
        {
            public static int Read(State state) => state switch
            {
                { IsIdle: true } => 0,
                { IsMoving: true, moving: (Point destination, var speed) } => 1,
            };
        }
        """;

    private const string TypedNullableIntCapture = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Choice
        {
            [Variant] public static partial Choice Number(int? value);
            [Variant] public static partial Choice Empty();
        }

        public static class Consumer
        {
            public static int Read(Choice choice) => choice switch
            {
                { IsNumber: true, number: { value: int value } } => value,
                { IsEmpty: true } => 0,
            };
        }
        """;

    private const string GenericCustomDeconstructorCapture = """
        #nullable enable
        using Houtamelo.Spire;

        public readonly struct SpecificValue
        {
            public void Deconstruct(out int value) => value = 0;
        }

        [DiscriminatedUnion]
        public partial struct GenericState<T> where T : struct
        {
            [Variant] public static partial GenericState<T> Value(T value);
            [Variant] public static partial GenericState<T> Empty();
        }

        public static class Consumer
        {
            public static int Read<T>(GenericState<T> state)
                where T : struct => state switch
            {
                { IsValue: true, value: { value: SpecificValue(var _) } } => 1,
                { IsEmpty: true } => 0,
            };
        }
        """;

    private const string OpenSubtypeCapture = """
        #nullable enable
        using Houtamelo.Spire;

        public abstract class Effect { }
        public sealed class DamageEffect : Effect { }

        public readonly struct Point { }

        [DiscriminatedUnion]
        public partial struct State
        {
            [Variant] public static partial State Idle();
            [Variant] public static partial State Moving(Point destination, Effect marker);
            [Variant] public static partial State Speaking(string text, float timer);
        }

        public static class Consumer
        {
            public static int Read(State state) => state switch
            {
                { IsIdle: true } => 0,
                { IsMoving: true, moving: (Point destination, DamageEffect marker) } => 1,
                { IsSpeaking: true, speaking: var (text, timer) } => 2,
            };
        }
        """;

    private const string ExtensionDeconstructSelectedOutputMismatch = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Choice
        {
            [Variant] public static partial Choice Flag(bool value);
            [Variant] public static partial Choice Empty();
        }

        public static class PayloadExtensions
        {
            public static void Deconstruct(
                this ChoiceTypes.Variants.Flag payload,
                out bool first,
                out bool second)
            {
                first = payload.value;
                second = payload.value;
            }
        }

        public static class Consumer
        {
            public static int Read(Choice choice) => choice switch
            {
                { IsFlag: true, flag: (true, false) } => 1,
                { IsFlag: true, flag: (false, false) } => 2,
                { IsEmpty: true } => 0,
            };
        }
        """;

    private const string ExtensionDeconstructShortOutput = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Choice
        {
            [Variant] public static partial Choice Pair(bool first, bool second);
            [Variant] public static partial Choice Empty();
        }

        public static class PayloadExtensions
        {
            public static void Deconstruct(
                this ChoiceTypes.Variants.Pair payload,
                out bool value)
            {
                value = payload.first;
            }
        }

        public static class Consumer
        {
            public static int Read(Choice choice) => choice switch
            {
                { IsPair: true, pair: ChoiceTypes.Variants.Pair(true) } => 1,
                { IsEmpty: true } => 0,
            };
        }
        """;

    [Fact]
    public async Task TypedValuePayload_MissingSiblingReportsSpireAndLeavesCompilerWarning()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(MissingSibling);
        var spire = Assert.Single(diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("typed-payload-case.cs", spire.Location.SourceTree?.FilePath);
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task TypedValuePayload_CompleteVariantsHaveNoSpireAndSuppressCompilerWarning()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(Complete);
        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE009");
        AssertCompilerWarningsSuppressed(compilation, diagnostics);
    }

    [Fact]
    public async Task TypedValuePayload_TypedSecondCaptureMissingSiblingReportsSpire()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(
            TypedSecondValuePayloadMissingSibling);
        Assert.Contains(diagnostics, d => d.Id == "SPIRE009");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task TypedNullableIntCapture_ReportsMissingNullAndLeavesCompilerWarning()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(TypedNullableIntCapture);
        Assert.Contains(diagnostics, d => d.Id == "SPIRE009");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task GenericCustomDeconstructorCapture_RemainsIndeterminateAndUnsuppressed()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(
            GenericCustomDeconstructorCapture);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task OpenSubtypeCapture_RemainsIndeterminateAndUnsuppressed()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(OpenSubtypeCapture);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task ExtensionDeconstruct_SelectedOutputMismatch_RemainsUnsuppressed()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(
            ExtensionDeconstructSelectedOutputMismatch);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task ExtensionDeconstruct_ShortOutput_DoesNotIndexPastOneChild()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(
            ExtensionDeconstructShortOutput);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    [Fact]
    public async Task TypedNullableReferenceCapture_MissingNullReportsSpireAndLeavesCompilerWarning()
    {
        var (compilation, diagnostics) = await AnalyzeGeneratedAsync(NullableReferenceCapture);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        Assert.Contains(diagnostics, d => d.Id == "SPIRE009");
        AssertCompilerWarningRemains(compilation, diagnostics);
    }

    private static async Task<(
        Compilation Compilation,
        ImmutableArray<RoslynDiagnostic> Diagnostics)> AnalyzeGeneratedAsync(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "typed-payload-case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new ExhaustivenessAnalyzer(),
                    new CS8509Suppressor()),
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return (compilation, diagnostics);
    }

    private static void AssertCompilerWarningRemains(
        Compilation compilation,
        ImmutableArray<RoslynDiagnostic> diagnostics)
    {
        var originalWarnings = compilation.GetDiagnostics()
            .Where(IsCompilerWarning)
            .ToArray();
        Assert.NotEmpty(originalWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics, diagnostic =>
                IsSameLocation(diagnostic, original) &&
                !diagnostic.IsSuppressed);
        }
    }

    private static void AssertCompilerWarningsSuppressed(
        Compilation compilation,
        ImmutableArray<RoslynDiagnostic> diagnostics)
    {
        var originalWarnings = compilation.GetDiagnostics()
            .Where(IsCompilerWarning)
            .ToArray();
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics, diagnostic =>
                IsSameLocation(diagnostic, original) &&
                diagnostic.IsSuppressed);
        }

        Assert.DoesNotContain(diagnostics, diagnostic =>
            IsCompilerWarning(diagnostic) && !diagnostic.IsSuppressed);
    }

    private static bool IsCompilerWarning(RoslynDiagnostic diagnostic)
        => diagnostic.Id is "CS8509" or "CS8524" or "CS8655" &&
           diagnostic.Location.SourceTree?.FilePath == "typed-payload-case.cs";

    private static bool IsSameLocation(
        RoslynDiagnostic left,
        RoslynDiagnostic right)
        => left.Id == right.Id &&
           left.Location.SourceTree?.FilePath == right.Location.SourceTree?.FilePath &&
           left.Location.SourceSpan == right.Location.SourceSpan;
}
