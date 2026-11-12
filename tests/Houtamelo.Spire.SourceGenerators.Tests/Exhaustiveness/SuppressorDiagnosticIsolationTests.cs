using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

public sealed class SuppressorDiagnosticIsolationTests
{
    private const string UnionDefinitions = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Result
        {
            [Variant] public static partial Result Ok();
            [Variant] public static partial Result Err(Exception error);

            public static implicit operator Result(Exception exception) => Err(exception);
        }

        [DiscriminatedUnion]
        public partial struct Result<T>
        {
            [Variant] public static partial Result<T> Ok(T value);
            [Variant] public static partial Result<T> Err(Exception error);

            public static implicit operator Result<T>(Exception exception) => Err(exception);
            public static implicit operator Result<T>(T value) => Ok(value);
        }
        """;

    private const string SingleOperator = """
        public partial struct Result<T>
        {
            public static implicit operator Result(Result<T> result) => result switch
            {
                { IsOk: true } => Result.Ok(),
                { IsErr: true, err: var (error) } => error,
            };
        }
        """;

    private const string DuplicateOperator = """
        public partial struct Result<T>
        {
            public static implicit operator Result(Result<T> result) => result switch
            {
                { IsOk: true } => Result.Ok(),
                { IsErr: true, err: var (error) } => error,
            };

            public static implicit operator Result(Result<T> result) => result switch
            {
                { IsOk: true } => Result.Ok(),
                { IsErr: true, err: var (error) } => error,
            };
        }
        """;

    private const string DuplicateMethods = """
        public partial struct Result<T>
        {
            public static implicit operator Result(Result<T> result) => result switch
            {
                { IsOk: true } => Result.Ok(),
                { IsErr: true, err: var (error) } => error,
            };

            public static int DuplicateMethod() => 1;
            public static int DuplicateMethod() => 2;
        }
        """;

    private const string UnrelatedWarning = """
        public partial struct Result<T>
        {
            public static implicit operator Result(Result<T> result) => result switch
            {
                { IsOk: true } => Result.Ok(),
                { IsErr: true, err: var (error) } => error,
            };

            private static void WarningControl()
            {
                int unused;
            }
        }
        """;

    private const string ManualPayloadConversions = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Result
        {
            [Variant] public static partial Result Ok();
            [Variant] public static partial Result Err(Exception error);

            public static implicit operator Result(Exception exception) => Err(exception);
            public static implicit operator Result(ResultTypes.Variants.Ok _) => Ok();
            public static implicit operator Result(ResultTypes.Variants.Err err) => Err(err.error);
        }

        [DiscriminatedUnion]
        public partial struct Result<T>
        {
            [Variant] public static partial Result<T> Ok(T value);
            [Variant] public static partial Result<T> Err(Exception error);

            public static implicit operator Result<T>(Exception exception) => Err(exception);
            public static implicit operator Result<T>(T value) => Ok(value);
            public static implicit operator Result<T>(ResultTypes1.Variants.Err<T> err) => Err(err.error);
            public static implicit operator Result<T>(ResultTypes1.Variants.Ok<T> ok) => Ok(ok.value);
        }
        """;

    [Fact]
    public async Task DuplicateImplicitOperators_LeaveCS0557Unsuppressed()
    {
        var source = UnionDefinitions + DuplicateOperator;
        var result = await AnalyzeAsync(source);
        var withoutSuppressor = await AnalyzeAsync(source, includeSuppressor: false);

        var compilerErrors = result.Compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id)
            .Distinct()
            .ToArray();
        Assert.Equal(new[] { "CS0557" }, compilerErrors);
        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS0557");
        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS8509");
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS0557" && !d.IsSuppressed);
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS0557" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS8509" && d.IsSuppressed);
        AssertNoUnexpectedSuppressions(result.Diagnostics);
        AssertCompilerDiagnosticsUnchanged(withoutSuppressor.Diagnostics, result.Diagnostics);
    }

    [Fact]
    public async Task SingleImplicitOperator_SuppressesOnlyCompilerExhaustiveness()
    {
        var source = UnionDefinitions + SingleOperator;
        var result = await AnalyzeAsync(source);
        var withoutSuppressor = await AnalyzeAsync(source, includeSuppressor: false);

        Assert.DoesNotContain(result.Compilation.GetDiagnostics(), d => d.Id == "CS0557");
        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS8509");
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS8509" && d.IsSuppressed);
        AssertNoUnexpectedSuppressions(result.Diagnostics);
        Assert.DoesNotContain(result.Compilation.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);
        AssertCompilerDiagnosticsUnchanged(withoutSuppressor.Diagnostics, result.Diagnostics);
    }

    [Fact]
    public async Task DuplicateOrdinaryMethods_LeaveCS0111Unsuppressed()
    {
        var source = UnionDefinitions + DuplicateMethods;
        var result = await AnalyzeAsync(source);
        var withoutSuppressor = await AnalyzeAsync(source, includeSuppressor: false);

        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS0111");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS0111" && !d.IsSuppressed);
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS0111" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        AssertNoUnexpectedSuppressions(result.Diagnostics);
        AssertCompilerDiagnosticsUnchanged(withoutSuppressor.Diagnostics, result.Diagnostics);
    }

    [Fact]
    public async Task UnrelatedWarning_StaysUnsuppressedAlongsideExhaustiveness()
    {
        var source = UnionDefinitions + UnrelatedWarning;
        var result = await AnalyzeAsync(source);
        var withoutSuppressor = await AnalyzeAsync(source, includeSuppressor: false);

        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS0168");
        Assert.Contains(result.Compilation.GetDiagnostics(), d => d.Id == "CS8509");
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS0168" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS0168" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS8509" && d.IsSuppressed);
        AssertNoUnexpectedSuppressions(result.Diagnostics);
        AssertCompilerDiagnosticsUnchanged(withoutSuppressor.Diagnostics, result.Diagnostics);
    }

    [Fact]
    public async Task ManualPayloadConversions_LeaveGeneratedCS0557Unsuppressed()
    {
        var result = await AnalyzeAsync(ManualPayloadConversions);
        var withoutSuppressor = await AnalyzeAsync(
            ManualPayloadConversions,
            includeSuppressor: false);
        var compilerDiagnostics = result.Compilation.GetDiagnostics();
        var duplicateConversions = compilerDiagnostics
            .Where(d => d.Id == "CS0557")
            .ToArray();

        Assert.Equal(4, duplicateConversions.Length);
        Assert.All(duplicateConversions, diagnostic =>
            Assert.EndsWith(
                ".g.cs",
                diagnostic.Location.SourceTree?.FilePath,
                System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(withoutSuppressor.Diagnostics,
            d => d.Id == "CS0557" && !d.IsSuppressed);
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS0557" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS0557" && d.IsSuppressed);
        AssertNoUnexpectedSuppressions(result.Diagnostics);
        AssertCompilerDiagnosticsUnchanged(withoutSuppressor.Diagnostics, result.Diagnostics);
    }

    private static async Task<(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics)> AnalyzeAsync(
        string source,
        bool includeSuppressor = true)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "suppressor-isolation-case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        var analyzers = includeSuppressor
            ? ImmutableArray.Create<DiagnosticAnalyzer>(
                new ExhaustivenessAnalyzer(),
                new CS8509Suppressor())
            : ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return (compilation, diagnostics.AddRange(generatorDiagnostics));
    }

    private static void AssertCompilerDiagnosticsUnchanged(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> withoutSuppressor,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> withSuppressor)
    {
        static ImmutableArray<string> Keys(
            ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
            => diagnostics
                .Where(d => !d.IsSuppressed
                    && d.Severity != DiagnosticSeverity.Hidden
                    && d.Id is not "CS8509" and not "CS8524"
                    && d.Location.IsInSource)
                .Select(d =>
                    $"{d.Id}|{d.Location.SourceTree?.FilePath}|" +
                    $"{d.Location.SourceSpan.Start}|{d.Location.SourceSpan.Length}")
                .OrderBy(key => key, System.StringComparer.Ordinal)
                .ToImmutableArray();

        Assert.Equal(Keys(withoutSuppressor).ToArray(), Keys(withSuppressor).ToArray());
    }

    private static void AssertNoUnexpectedSuppressions(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        => Assert.DoesNotContain(
            diagnostics,
            d => d.IsSuppressed && d.Id is not "CS8509" and not "CS8524");
}
