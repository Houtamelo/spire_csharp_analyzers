using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

/// Integration coverage for nullable reference payloads in generated struct DUs.
/// The payload's ordinary reference type is intentionally not an exhaustive
/// hierarchy; only its annotated null/non-null split is finite.
public sealed class NullablePayloadIntegrationTests
{
    private const string NullPayloadOnly = """
        #nullable enable
        using Houtamelo.Spire;

        public abstract class Effect { }
        public sealed class DamageEffect : Effect { }

        [DiscriminatedUnion]
        public partial struct GameplayCommand
        {
            [Variant] public static partial GameplayCommand Idle();
            [Variant] public static partial GameplayCommand ApplyEffect(
                [UnitySerializeReference] Effect? effect);
        }

        public static class Consumer
        {
            public static int Read(GameplayCommand command) => command switch
            {
                { kind: GameplayCommand.Kind.Idle } => 0,
                { kind: GameplayCommand.Kind.ApplyEffect,
                  applyEffect: { effect: null } } => 1,
            };
        }
        """;

    private const string NonNullPayloadOnly = """
        #nullable enable
        using Houtamelo.Spire;

        public abstract class Effect { }
        public sealed class DamageEffect : Effect { }

        [DiscriminatedUnion]
        public partial struct GameplayCommand
        {
            [Variant] public static partial GameplayCommand Idle();
            [Variant] public static partial GameplayCommand ApplyEffect(
                [UnitySerializeReference] Effect? effect);
        }

        public static class Consumer
        {
            public static int Read(GameplayCommand command) => command switch
            {
                { kind: GameplayCommand.Kind.Idle } => 0,
                { kind: GameplayCommand.Kind.ApplyEffect,
                  applyEffect: { effect: not null } } => 1,
            };
        }
        """;

    [Fact]
    public async Task NullOnly_ReportsMissingNonNullAndCarriesCorrelatedPattern()
    {
        var (compilation, diagnostic) = await GetMissingDiagnosticAsync(NullPayloadOnly);
        Assert.Equal("SPIRE009", diagnostic.Id);
        Assert.Equal("false", diagnostic.Properties["UseVariantTemplates"]);
        Assert.Equal("ApplyEffect", diagnostic.Properties["MissingVariants"]);
        Assert.True(int.Parse(diagnostic.Properties["MissingPatternCount"]!) > 0);
        var patterns = GetMissingPatterns(diagnostic);
        Assert.Equal(
            "{ IsApplyEffect: true, applyEffect: { effect: { } } }",
            Assert.Single(patterns));

        var fixedSource = await ApplyFixAsync(compilation, diagnostic);
        Assert.Contains("effect: null", fixedSource, StringComparison.Ordinal);
        Assert.Contains("ApplyEffect", fixedSource, StringComparison.Ordinal);
        var fixedCompilation = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedCompilation);
        AssertEmits(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, candidate => candidate.Id == "SPIRE009");
    }

    [Fact]
    public async Task NonNullOnly_ReportsMissingNullAndCodeFixClearsDiagnostic()
    {
        var (compilation, diagnostic) = await GetMissingDiagnosticAsync(NonNullPayloadOnly);
        Assert.Equal("false", diagnostic.Properties["UseVariantTemplates"]);
        Assert.Equal("ApplyEffect", diagnostic.Properties["MissingVariants"]);
        var patterns = GetMissingPatterns(diagnostic);
        Assert.Equal(
            "{ IsApplyEffect: true, applyEffect: { effect: null } }",
            Assert.Single(patterns));

        var fixedSource = await ApplyFixAsync(compilation, diagnostic);
        Assert.Contains("effect: null", fixedSource, StringComparison.Ordinal);
        var fixedCompilation = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedCompilation);
        AssertEmits(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, candidate => candidate.Id == "SPIRE009");
    }

    [Fact]
    public async Task BothNullablePartitionsHaveNoSpireDiagnostic()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;
            public abstract class Effect { }
            [DiscriminatedUnion]
            public partial struct GameplayCommand
            {
                [Variant] public static partial GameplayCommand Idle();
                [Variant] public static partial GameplayCommand ApplyEffect(
                    [UnitySerializeReference] Effect? effect);
            }
            public static class Consumer
            {
                public static int Read(GameplayCommand command) => command switch
                {
                    { kind: GameplayCommand.Kind.Idle } => 0,
                    { kind: GameplayCommand.Kind.ApplyEffect,
                      applyEffect: { effect: null } } => 1,
                    { kind: GameplayCommand.Kind.ApplyEffect,
                      applyEffect: { effect: not null } } => 2,
                };
            }
            """;

        var (compilation, _) = RunGenerated(source);
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, candidate => candidate.Id == "SPIRE009");
    }

    [Fact]
    public async Task IsVariantPredicate_NullOnlyStillReportsMissingNonNull()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;
            public abstract class Effect { }
            [DiscriminatedUnion]
            public partial struct GameplayCommand
            {
                [Variant] public static partial GameplayCommand Idle();
                [Variant] public static partial GameplayCommand ApplyEffect(
                    [UnitySerializeReference] Effect? effect);
            }
            public static class Consumer
            {
                public static int Read(GameplayCommand command) => command switch
                {
                    { IsIdle: true } => 0,
                    { IsApplyEffect: true,
                      applyEffect: { effect: null } } => 1,
                };
            }
            """;

        var (compilation, diagnostic) = await GetMissingDiagnosticAsync(source);
        Assert.Equal("false", diagnostic.Properties["UseVariantTemplates"]);
        Assert.Equal("ApplyEffect", diagnostic.Properties["MissingVariants"]);
        Assert.Equal(
            "{ IsApplyEffect: true, applyEffect: { effect: { } } }",
            Assert.Single(GetMissingPatterns(diagnostic)));
        var fixedSource = await ApplyFixAsync(compilation, diagnostic);
        var fixedCompilation = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedCompilation);
        AssertEmits(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, candidate => candidate.Id == "SPIRE009");
    }

    [Fact]
    public async Task ExplicitNullablePayloadInDisabledContextStillReportsMissingComplement()
    {
        var source = NullPayloadOnly.Replace(
            "#nullable enable",
            "#nullable disable",
            StringComparison.Ordinal);
        var (_, diagnostic) = await GetMissingDiagnosticAsync(source);
        Assert.Contains("{ IsApplyEffect: true, applyEffect: { effect: { } } }", GetMissingPatterns(diagnostic));
    }

    [Fact]
    public async Task MissingNullablePartitionsRemainUnsuppressedAndCompletePartitionsSuppress()
    {
        await AssertCompilerWarningRemains(NullPayloadOnly);
        await AssertCompilerWarningRemains(NonNullPayloadOnly);

        const string complete = """
            #nullable enable
            using Houtamelo.Spire;
            public abstract class Effect { }
            [DiscriminatedUnion]
            public partial struct GameplayCommand
            {
                [Variant] public static partial GameplayCommand Idle();
                [Variant] public static partial GameplayCommand ApplyEffect(
                    [UnitySerializeReference] Effect? effect);
            }
            public static class Consumer
            {
                public static int Read(GameplayCommand command) => command switch
                {
                    { kind: GameplayCommand.Kind.Idle } => 0,
                    { kind: GameplayCommand.Kind.ApplyEffect,
                      applyEffect: { effect: null } } => 1,
                    { kind: GameplayCommand.Kind.ApplyEffect,
                      applyEffect: { effect: not null } } => 2,
                };
            }
            """;
        await AssertCompilerWarningSuppressed(complete);
    }

    private static async Task<(Compilation Compilation, RoslynDiagnostic Diagnostic)>
        GetMissingDiagnosticAsync(string source)
    {
        var (compilation, _) = RunGenerated(source);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics.Where(candidate => candidate.Id == "SPIRE009"));
        return (compilation, diagnostic);
    }

    private static async Task AssertCompilerWarningRemains(string source)
    {
        var (compilation, _) = RunGenerated(source);
        var original = compilation.GetDiagnostics()
            .Where(IsCompilerExhaustivenessWarning)
            .ToArray();
        Assert.NotEmpty(original);
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
        Assert.Contains(diagnostics, diagnostic =>
            IsCompilerExhaustivenessWarning(diagnostic) && !diagnostic.IsSuppressed);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
    }

    private static async Task AssertCompilerWarningSuppressed(string source)
    {
        var (compilation, _) = RunGenerated(source);
        var original = compilation.GetDiagnostics()
            .Where(IsCompilerExhaustivenessWarning)
            .ToArray();
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
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
        foreach (var originalDiagnostic in original)
        {
            Assert.Contains(diagnostics, diagnostic =>
                diagnostic.Id == originalDiagnostic.Id &&
                diagnostic.Location.SourceSpan == originalDiagnostic.Location.SourceSpan &&
                diagnostic.IsSuppressed);
        }
    }

    private static bool IsCompilerExhaustivenessWarning(RoslynDiagnostic diagnostic)
        => diagnostic.Id is "CS8509" or "CS8524" or "CS8655";

    private static void AssertEmits(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static string[] GetMissingPatterns(RoslynDiagnostic diagnostic)
    {
        var count = int.Parse(diagnostic.Properties["MissingPatternCount"]!);
        return Enumerable.Range(0, count)
            .Select(index => diagnostic.Properties[$"MissingPattern{index}"]!)
            .ToArray();
    }

    private static (Compilation Compilation, GeneratorDriverRunResult Result)
        RunGenerated(string source)
    {
        var result = GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "nullable-payload-case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        return (compilation, result);
    }

    private static Compilation RunGeneratedCompilation(string source)
    {
        var (compilation, _) = RunGenerated(source);
        return compilation;
    }

    private static async Task<string> ApplyFixAsync(
        Compilation compilation,
        RoslynDiagnostic diagnostic)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "NullablePayloadCodeFix", "NullablePayloadCodeFix", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options);
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocumentId = null;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, Path.GetFileName(tree.FilePath), tree.GetText());
            if (tree.FilePath.EndsWith("nullable-payload-case.cs", StringComparison.Ordinal))
                userDocumentId = documentId;
        }

        Assert.NotNull(userDocumentId);
        var document = solution.GetDocument(userDocumentId!)!;
        var actions = new List<CodeAction>();
        await new AddMissingArmsCodeFix().RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        return (await apply.ChangedSolution.GetDocument(userDocumentId!)!.GetTextAsync()).ToString();
    }
}
