using System;
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

public sealed class CaptureDeconstructionTests
{
    [Fact]
    public async Task GeneratedValuePayload_AllCaptureExpression_SuppressesCompilerWarningsAndPreservesOrder()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Number(int value);
                [Variant] public static partial Choice Pair(string? first, int second);
                [Variant] public static partial Choice Empty();
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Number, number: var (value) } => value,
                    { kind: Choice.Kind.Pair, pair: var (first, second) } =>
                        (first?.Length ?? -1) * 100 + second,
                    { kind: Choice.Kind.Empty } => 0,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertCompleteExpression(result);

        var assembly = EmitAndLoad(result.Compilation);
        var choiceType = assembly.GetType("Choice", throwOnError: true)!;
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;

        var number = choiceType.GetMethod("Number", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { 17 });
        var pair = choiceType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { null, 7 });
        var empty = choiceType.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, Array.Empty<object>());

        Assert.Equal(17, read.Invoke(null, new[] { number }));
        Assert.Equal(-93, read.Invoke(null, new[] { pair }));
        Assert.Equal(0, read.Invoke(null, new[] { empty }));
    }

    [Fact]
    public async Task GeneratedValuePayload_ParenthesizedDiscardsAreUniversal()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Number(int value);
                [Variant] public static partial Choice Pair(string? first, int second);
                [Variant] public static partial Choice Empty();
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Number, number: var (_) } => 1,
                    { kind: Choice.Kind.Pair, pair: var (first, _) } => first?.Length ?? -1,
                    { kind: Choice.Kind.Empty } => 0,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertCompleteExpression(result);

        var assembly = EmitAndLoad(result.Compilation);
        var choiceType = assembly.GetType("Choice", throwOnError: true)!;
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;
        var number = choiceType.GetMethod("Number", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { 17 });
        var pair = choiceType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { "abc", 7 });

        Assert.Equal(1, read.Invoke(null, new[] { number }));
        Assert.Equal(3, read.Invoke(null, new[] { pair }));
    }

    [Fact]
    public async Task GeneratedValuePayload_AllCaptureStatement_IsExhaustiveAndExecutable()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Message
            {
                [Variant] public static partial Message Number(int value);
                [Variant] public static partial Message Pair(string? first, int second);
                [Variant] public static partial Message Empty();
            }

            public static class Reader
            {
                public static int Read(Message message)
                {
                    switch (message)
                    {
                        case { kind: Message.Kind.Number, number: var (value) }:
                            return value;
                        case { kind: Message.Kind.Pair, pair: var (first, second) }:
                            return (first?.Length ?? -1) * 100 + second;
                        case { kind: Message.Kind.Empty }:
                            return 0;
                    }

                    return -1;
                }
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertNoRuleDiagnostics(result);

        var assembly = EmitAndLoad(result.Compilation);
        var messageType = assembly.GetType("Message", throwOnError: true)!;
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;
        var number = messageType.GetMethod("Number", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { 23 });
        var pair = messageType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { null, 5 });
        var empty = messageType.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, Array.Empty<object>());

        Assert.Equal(23, read.Invoke(null, new[] { number }));
        Assert.Equal(-95, read.Invoke(null, new[] { pair }));
        Assert.Equal(0, read.Invoke(null, new[] { empty }));
    }

    [Fact]
    public async Task GeneratedGenericNullablePayload_CapturesNullAndPreservesTypes()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Maybe<T> where T : class
            {
                [Variant] public static partial Maybe<T> Empty();
                [Variant] public static partial Maybe<T> Some(T? value);
                [Variant] public static partial Maybe<T> Pair(T? first, int second);
            }

            public static class Reader
            {
                public static int Read(Maybe<string> maybe) => maybe switch
                {
                    { kind: Maybe<string>.Kind.Empty } => 0,
                    { kind: Maybe<string>.Kind.Some, some: var (value) } =>
                        value is null ? 1 : value.Length + 10,
                    { kind: Maybe<string>.Kind.Pair, pair: var (first, second) } =>
                        (first is null ? -100 : first.Length) + second,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertCompleteExpression(result);

        var assembly = EmitAndLoad(result.Compilation);
        var maybeType = assembly.GetType("Maybe`1", throwOnError: true)!
            .MakeGenericType(typeof(string));
        var readerType = assembly.GetType("Reader", throwOnError: true)!;
        var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;
        var empty = maybeType.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, Array.Empty<object>());
        var someNull = maybeType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { null });
        var pairNull = maybeType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { null, 4 });
        var pairValue = maybeType.GetMethod("Pair", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { "abc", 4 });

        Assert.Equal(0, read.Invoke(null, new[] { empty }));
        Assert.Equal(1, read.Invoke(null, new[] { someNull }));
        Assert.Equal(-96, read.Invoke(null, new[] { pairNull }));
        Assert.Equal(7, read.Invoke(null, new[] { pairValue }));
    }

    [Fact]
    public async Task GeneratedPayload_MissingRootVariant_ReportsSPIRE009AndLeavesCompilerWarning()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha(int value);
                [Variant] public static partial Choice Beta(string text);
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Alpha, alpha: var (value) } => value,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertIncompleteExpression(result);
    }

    [Fact]
    public async Task GeneratedPayload_MissingRootVariantInStatement_ReportsSPIRE009()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha(int value);
                [Variant] public static partial Choice Beta(string text);
            }

            public static class Reader
            {
                public static int Read(Choice choice)
                {
                    switch (choice)
                    {
                        case { kind: Choice.Kind.Alpha, alpha: var (value) }:
                            return value;
                    }

                    return -1;
                }
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
    }

    [Fact]
    public async Task GeneratedPayload_ConstantPositionalRestriction_RemainsIncomplete()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Pair(int first, int second);
                [Variant] public static partial Choice Empty();
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Pair, pair: (0, var second) } => second,
                    { kind: Choice.Kind.Empty } => 0,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertRestrictedExpression(result);
    }

    [Fact]
    public async Task GeneratedPayload_TypedPositionalRestriction_RemainsIncomplete()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Pair(object value, int second);
                [Variant] public static partial Choice Empty();
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Pair, pair: (string text, var second) } => text.Length + second,
                    { kind: Choice.Kind.Empty } => 0,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertRestrictedExpression(result);
    }

    [Fact]
    public async Task GeneratedPayload_NestedPositionalRestriction_RemainsIncomplete()
    {
        const string source = """
            using Houtamelo.Spire;

            public sealed class Inner
            {
                public bool Flag { get; }
                public Inner(bool flag) => Flag = flag;
            }

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Pair(Inner inner, int value);
                [Variant] public static partial Choice Empty();
            }

            public static class Reader
            {
                public static int Read(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Pair, pair: (Inner { Flag: true }, var value) } => value,
                    { kind: Choice.Kind.Empty } => 0,
                };
            }
            """;

        var result = await AnalyzeGeneratedAsync(source);
        AssertRestrictedExpression(result);
    }

    private static void AssertCompleteExpression(AnalysisResult result)
    {
        var originalWarnings = result.OriginalDiagnostics
            .Where(IsCompilerExhaustivenessDiagnostic)
            .ToArray();
        Assert.NotEmpty(originalWarnings);

        AssertNoRuleDiagnostics(result);
        Assert.DoesNotContain(result.Diagnostics,
            diagnostic => IsCompilerExhaustivenessDiagnostic(diagnostic) && !diagnostic.IsSuppressed);

        foreach (var original in originalWarnings)
        {
            Assert.Contains(result.Diagnostics,
                diagnostic => IsSameDiagnostic(diagnostic, original) && diagnostic.IsSuppressed);
        }
    }

    private static void AssertNoRuleDiagnostics(AnalysisResult result)
    {
        Assert.DoesNotContain(result.Diagnostics,
            diagnostic => diagnostic.Id is "SPIRE009" or "SPIRE013" or "SPIRE014");
    }

    private static void AssertIncompleteExpression(AnalysisResult result)
    {
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "SPIRE009");

        var originalWarnings = result.OriginalDiagnostics
            .Where(IsCompilerExhaustivenessDiagnostic)
            .ToArray();
        Assert.NotEmpty(originalWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(result.Diagnostics,
                diagnostic => IsSameDiagnostic(diagnostic, original) && !diagnostic.IsSuppressed);
        }
    }

    private static void AssertRestrictedExpression(AnalysisResult result)
    {
        var originalWarnings = result.OriginalDiagnostics
            .Where(IsCompilerExhaustivenessDiagnostic)
            .ToArray();
        Assert.NotEmpty(originalWarnings);

        Assert.DoesNotContain(result.Diagnostics,
            diagnostic => IsCompilerExhaustivenessDiagnostic(diagnostic) && diagnostic.IsSuppressed);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(result.Diagnostics,
                diagnostic => IsSameDiagnostic(diagnostic, original) && !diagnostic.IsSuppressed);
        }
    }

    private static bool IsCompilerExhaustivenessDiagnostic(
        Microsoft.CodeAnalysis.Diagnostic diagnostic)
        => diagnostic.Id is "CS8509" or "CS8524";

    private static bool IsSameDiagnostic(
        Microsoft.CodeAnalysis.Diagnostic left,
        Microsoft.CodeAnalysis.Diagnostic right)
        => left.Id == right.Id &&
           string.Equals(
               left.Location.SourceTree?.FilePath,
               right.Location.SourceTree?.FilePath,
               StringComparison.Ordinal) &&
           left.Location.SourceSpan.Equals(right.Location.SourceSpan);

    private static async Task<AnalysisResult> AnalyzeGeneratedAsync(string source)
    {
        var generated = Generate(source);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generated.GeneratorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generated.Compilation);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new FieldAccessSafetyAnalyzer(),
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor());
        var withAnalyzers = generated.Compilation.WithAnalyzers(
            analyzers,
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));
        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");

        return new AnalysisResult(
            generated.Compilation,
            generated.Compilation.GetDiagnostics(),
            diagnostics);
    }

    private static GeneratedResult Generate(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "capture-deconstruction.cs");
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
            $"CaptureDeconstruction_{Guid.NewGuid():N}",
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

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success,
            string.Join("\n", emit.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private sealed record GeneratedResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics);

    private sealed record AnalysisResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> OriginalDiagnostics,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics);
}
