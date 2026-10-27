using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

/// Exhaustiveness coverage for generated Is{Variant} discriminator predicates.
public sealed class IsVariantExhaustivenessTests
{
    [Fact]
    public async Task AllVariantsUseIsTrue_NoExhaustivenessDiagnostic()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsIdle: true } => 0,
                    { IsMove: true, move: var (value) } => value,
                    { IsStop: true } => 2,
                };
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task MissingIsTrueVariant_ReportsMissingVariant()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsIdle: true } => 0,
                    { IsMove: true, move: var (value) } => value,
                };
            }
            """));

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Stop", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task IsFalseComplementAndIsTrueVariant_AreExhaustive()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: false } => 0,
                    { IsMove: true, move: var (value) } => value,
                };
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task OnlyIsFalseComplement_ReportsTheExcludedVariant()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: false } => 0,
                };
            }
            """));

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Move", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task MixedKindAndIsPatterns_AreExhaustive()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { kind: Command.Kind.Idle } => 0,
                    { IsMove: true, move: var (value) } => value,
                    { IsStop: true } => 2,
                };
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task IsPayloadDeconstruction_PreservesFieldOrderAndNullCaptures()
    {
        var result = await AnalyzeGenerated("""
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Message
            {
                [Variant] public static partial Message Number(int value);
                [Variant] public static partial Message Pair(string? first, int second);
                [Variant] public static partial Message Empty();
            }

            static class Reader
            {
                public static int Read(Message message) => message switch
                {
                    { IsNumber: true, number: var (value) } => value,
                    { IsPair: true, pair: var (first, second) } =>
                        (first?.Length ?? -1) * 100 + second,
                    { IsEmpty: true } => 0,
                };
            }
            """);

        AssertNoSurvivingExhaustiveness(result);
        AssertNoUnsuppressedCompilerWarnings(result);

        using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        Assert.True(emit.Success,
            string.Join("\n", emit.Diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));

        var assembly = TestHarnessEnvironment.LoadAssembly(stream.ToArray());
        var messageType = assembly.GetType("Message", throwOnError: true)!;
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;
        var number = messageType.GetMethod("Number", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { 23 });
        var pairNull = messageType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { null, 5 });
        var pairValue = messageType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { "abc", 4 });
        var empty = messageType.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, Array.Empty<object>());

        Assert.Equal(23, read.Invoke(null, new[] { number }));
        Assert.Equal(-95, read.Invoke(null, new[] { pairNull }));
        Assert.Equal(304, read.Invoke(null, new[] { pairValue }));
        Assert.Equal(0, read.Invoke(null, new[] { empty }));
    }

    [Fact]
    public async Task RestrictedIsPayloadPattern_DoesNotCoverTheWholeVariant()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsIdle: true } => 0,
                    { IsMove: true, move: { value: 0 } } => 1,
                    { IsStop: true } => 2,
                };
            }
            """));

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Move", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task ImpossibleCombinedPredicates_DoNotCountAsCoverage()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: true, IsStop: true } => 1,
                    { IsMove: false } => 0,
                };
            }
            """));

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Move", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task NegatedBooleanPropertyPatterns_CoverBothValues()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: not true } => 0,
                    { IsMove: not false, move: var (value) } => value,
                };
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task NullableRoot_NullAndIsPatternsRemainExhaustive()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command? command) => command switch
                {
                    null => -1,
                    { IsIdle: true } => 0,
                    { IsMove: true, move: var (value) } => value,
                    { IsStop: true } => 2,
                };
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task SwitchStatement_AllVariantsUseIsTrue_NoDiagnostic()
    {
        var result = await AnalyzeGenerated(CommandSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    switch (command)
                    {
                        case { IsIdle: true }:
                            return 0;
                        case { IsMove: true, move: var (value) }:
                            return value;
                        case { IsStop: true }:
                            return 2;
                    }

                    return -1;
                }
            }
            """));

        AssertNoSurvivingExhaustiveness(result);
    }

    [Fact]
    public async Task ExtendedPredicateOnNestedUnion_DoesNotProveRootVariant()
    {
        var result = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            public sealed class Box
            {
                public Command command;
            }

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(Box box);
                [Variant] public static partial Command Stop();
            }

            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { kind: Command.Kind.Move, move.box.command.IsMove: true } => 1,
                    { IsStop: true } => 0,
                };
            }
            """);

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Move", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task GenericUnion_UsesIsPredicateForClosedConstruction()
    {
        var result = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Option<T>
            {
                [Variant] public static partial Option<T> Some(T value);
                [Variant] public static partial Option<T> None();
            }

            static class Reader
            {
                public static int Read(Option<int> option) => option switch
                {
                    { IsSome: true, some: var (value) } => value,
                };
            }
            """);

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("None", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task UnrelatedIsFooProperty_DoesNotCoverUnionVariants()
    {
        var result = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int value);
                [Variant] public static partial Command Stop();

                public bool IsFoo => true;
            }

            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsFoo: true } => 0,
                };
            }
            """);

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Move,Stop", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task RecordUnion_RemainsTypePatternBased()
    {
        var result = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public abstract partial record Result
            {
                public sealed partial record Ok(int Value) : Result;
                public sealed partial record Error(string Message) : Result;
            }

            static class Reader
            {
                public static int Read(Result result) => result switch
                {
                    Result.Ok ok => ok.Value,
                    null => -1,
                };
            }
            """);

        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Error", diagnostic.Properties["MissingVariants"]);
    }

    [Fact]
    public async Task IsPatterns_EmitAndExecutePayloadValues()
    {
        var source = CommandSource("""
            public static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsIdle: true } => 10,
                    { IsMove: true, move: var (value) } => value,
                    { IsStop: true } => 30,
                };
            }
            """);

        var result = await AnalyzeGenerated(source);
        AssertNoSurvivingExhaustiveness(result);

        using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        Assert.True(emit.Success,
            string.Join("\n", emit.Diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));

        var assembly = TestHarnessEnvironment.LoadAssembly(stream.ToArray());
        var commandType = assembly.GetType("Command", throwOnError: true)!;
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var move = commandType.GetMethod("Move", BindingFlags.Public | BindingFlags.Static)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(10, read.Invoke(null, new[] { Activator.CreateInstance(commandType, nonPublic: true)! }));
        Assert.Equal(42, read.Invoke(null, new[] { move.Invoke(null, new object[] { 42 })! }));
    }

    private static void AssertNoSurvivingExhaustiveness(AnalysisResult result)
    {
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
        AssertNoUnsuppressedCompilerWarnings(result);
    }

    private static void AssertNoUnsuppressedCompilerWarnings(AnalysisResult result)
    {
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id is "CS8509" or "CS8524" && !d.IsSuppressed);
    }

    private static async Task<AnalysisResult> AnalyzeGenerated(string source)
    {
        var generated = Generate(source);
        var outputCompilation = generated.Compilation;
        var generatorDiagnostics = generated.GeneratorDiagnostics;
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var originalDiagnostics = outputCompilation.GetDiagnostics();
        var withAnalyzers = outputCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(
                new ExhaustivenessAnalyzer(),
                new CS8509Suppressor()),
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));
        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");

        return new AnalysisResult(outputCompilation, originalDiagnostics, diagnostics);
    }

    private static GeneratedResult Generate(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "is-variant-case.cs");
        var references = TestHarnessEnvironment.RuntimeReferences
            .Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location));
        var syntaxTrees = ImmutableArray.Create<SyntaxTree>(tree);
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
            parseOptions,
            references);
        if (compatibilityTree is not null)
            syntaxTrees = syntaxTrees.Add(compatibilityTree);

        var compilation = CSharpCompilation.Create(
            $"IsVariantExhaustiveness_{Guid.NewGuid():N}",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create(
                new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

        _ = driver;
        return new GeneratedResult(outputCompilation, generatorDiagnostics);
    }

    private static string CommandSource(string consumer)
        => """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Move(int value);
                [Variant] public static partial Command Stop();
            }
            """ + consumer;

    private readonly record struct AnalysisResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> OriginalDiagnostics,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics);

    private readonly record struct GeneratedResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics);
}
