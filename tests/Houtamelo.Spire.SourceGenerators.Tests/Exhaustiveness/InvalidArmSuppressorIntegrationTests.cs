using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

public sealed class InvalidArmSuppressorIntegrationTests
{
    private const string InvalidInvocation = @"
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Result<T>
        {
            [Variant] public static partial Result<T> Ok(T value);
            [Variant] public static partial Result<T> Err(Exception err);
        }

        public partial struct Result<T>
        {
            public TMap MapOrElse<TMap>(
                Func<T, TMap> fMap,
                Func<Exception, TMap> fElse) => this switch
            {
                { IsOk: true, ok: var (value) } => fMap(value),
                { IsErr: true } => fElse(),
            };
        }
        ";

    private const string DefaultArm = @"
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Result<T>
        {
            [Variant] public static partial Result<T> Ok(T value);
            [Variant] public static partial Result<T> Err(Exception err);
        }

        public partial struct Result<T>
        {
            public TMap MapOrElse<TMap>(
                Func<T, TMap> fMap,
                Func<Exception, TMap> fElse) => this switch
            {
                { IsOk: true, ok: var (value) } => fMap(value),
                { IsErr: true } => default,
            };
        }
        ";

    [Fact]
    public async Task InvalidArmInvocation_ReportsOnlyInvocationError()
    {
        var result = await AnalyzeAsync(InvalidInvocation);
        var original = result.Compilation.GetDiagnostics();
        var errors = original
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        var error = Assert.Single(errors);
        Assert.Equal("CS7036", error.Id);
        Assert.DoesNotContain(original, d => d.Id == "CS8509");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8509");
    }

    [Fact]
    public async Task DefaultArm_CompilerExhaustivenessWarningIsSuppressed()
    {
        var result = await AnalyzeAsync(DefaultArm);
        var original = result.Compilation.GetDiagnostics();

        Assert.DoesNotContain(original, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(original, d => d.Id == "CS8509");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS8509" && d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
    }

    [Fact]
    public async Task CapturedErrArm_IsExhaustiveAndHasNoInvocationError()
    {
        var source = InvalidInvocation.Replace(
            "{ IsErr: true } => fElse()",
            "{ IsErr: true, err: var (error) } => fElse(error)");
        var result = await AnalyzeAsync(source);

        var original = result.Compilation.GetDiagnostics();
        Assert.DoesNotContain(original, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(original, d => d.Id == "CS8509");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS8509" && d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509" && !d.IsSuppressed);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
    }

    [Fact]
    public async Task ScopedPragma_InvalidArmPreservesOnlyInvocationError()
    {
        var result = await AnalyzeAsync(AddScopedCs8509Pragma(InvalidInvocation));
        var errors = result.Compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        var error = Assert.Single(errors);
        Assert.Equal("CS7036", error.Id);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "AD0001");
    }

    [Fact]
    public async Task ScopedPragma_IncompleteUnionKeepsSpireDiagnosticAndOutsideWarning()
    {
        var source = BuildScopedIncompleteSource();
        var result = await AnalyzeAsync(source);
        var incompleteLine = FindLine(source, "Func<Exception, TMap> fElse) => this switch");
        var unrelatedLine = FindLine(source, "Describe(int value) => value switch");

        Assert.DoesNotContain(result.Compilation.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);

        var spire = Assert.Single(result.Diagnostics.Where(d => d.Id == "SPIRE009"));
        Assert.Equal("Err", spire.Properties["MissingVariants"]);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "CS8509"
                && !d.IsSuppressed
                && d.Location.GetLineSpan().StartLinePosition.Line + 1 == incompleteLine);
        Assert.Contains(result.Diagnostics,
            d => d.Id == "CS8509"
                && !d.IsSuppressed
                && d.Location.GetLineSpan().StartLinePosition.Line + 1 == unrelatedLine);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "AD0001");
    }

    private const string UnrelatedIncompleteIntSwitch = @"
        public static class UnrelatedControl
        {
            public static string Describe(int value) => value switch
            {
                0 => ""zero"",
            };
        }
        ";

    private static string BuildScopedIncompleteSource()
    {
        var source = DefaultArm.Replace("\r\n", "\n").Replace(
            "                { IsErr: true } => default,\n",
            string.Empty);
        return AddScopedCs8509Pragma(source) + UnrelatedIncompleteIntSwitch;
    }

    private static string AddScopedCs8509Pragma(string source)
    {
        source = source.Replace("\r\n", "\n");
        const string methodStart =
            "        public partial struct Result<T>\n" +
            "        {\n" +
            "            public TMap MapOrElse";
        const string methodStartWithPragma =
            "        public partial struct Result<T>\n" +
            "        {\n" +
            "#pragma warning disable CS8509\n" +
            "            public TMap MapOrElse";
        const string methodEnd = "            };\n        ";
        const string methodEndWithPragma =
            "            };\n" +
            "#pragma warning restore CS8509\n" +
            "        ";

        var scoped = source.Replace(methodStart, methodStartWithPragma);
        scoped = scoped.Replace(methodEnd, methodEndWithPragma);
        return scoped;
    }

    private static int FindLine(string source, string marker)
        => source.Split('\n')
            .Select((line, index) => (line, index))
            .Single(item => item.line.Contains(marker, System.StringComparison.Ordinal)).index + 1;

    private static async Task<(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics)> AnalyzeAsync(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "invalid-arm-case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        var withAnalyzers = compilation.WithAnalyzers(
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
        return (compilation, diagnostics.AddRange(generatorDiagnostics));
    }
}
