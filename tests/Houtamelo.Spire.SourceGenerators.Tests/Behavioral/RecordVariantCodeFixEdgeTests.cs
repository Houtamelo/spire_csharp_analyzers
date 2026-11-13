using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Roslyn workspace coverage for authored record-variant serialization fixes.
public sealed class RecordVariantCodeFixEdgeTests
{
    [Fact]
    public async Task ComputedPositionalPropertyWithoutBackingField_DoesNotOfferFieldFix()
    {
        var run = RunUnity(new[] { ("computed.cs", ComputedPropertySource) });
        var diagnostics = await Analyze(run.OutputCompilation);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE028");
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "computed.cs");
        var actions = await GetActions(document, diagnostic);

        Assert.DoesNotContain(actions, action =>
            action.Title.Contains("serialization attributes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuthoredPropertyInSecondPartialDocument_FieldFixEditsOnlyThatDocumentAndRegenerates()
    {
        var sources = new[]
        {
            ("primary.cs", MultiPartPrimarySource),
            ("secondary.cs", MultiPartSecondarySource),
        };
        var run = RunUnity(sources);
        var diagnostics = await Analyze(run.OutputCompilation);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE028");
        var solution = CreateSolution(run.OutputCompilation);
        var primary = FindDocument(solution, "primary.cs");
        var secondary = FindDocument(solution, "secondary.cs");
        var actions = await GetActions(primary, diagnostic);
        var fieldAction = Assert.Single(actions, action =>
            action.Title.Contains("serialization attributes", StringComparison.OrdinalIgnoreCase));

        var operations = await fieldAction.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changedPrimary = apply.ChangedSolution.GetDocument(primary.Id)!;
        var changedSecondary = apply.ChangedSolution.GetDocument(secondary.Id)!;
        var primaryText = (await changedPrimary.GetTextAsync()).ToString();
        var secondaryText = (await changedSecondary.GetTextAsync()).ToString();

        Assert.Equal(MultiPartPrimarySource, primaryText);
        Assert.Contains("field: global::UnityEngine.SerializeField", secondaryText, StringComparison.Ordinal);

        var regenerated = RunUnity(new[]
        {
            ("primary.cs", primaryText),
            ("secondary.cs", secondaryText),
        });
        Assert.DoesNotContain(regenerated.GeneratorDiagnostics, diagnosticItem =>
            diagnosticItem.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(regenerated.OutputCompilation.GetDiagnostics(), diagnosticItem =>
            diagnosticItem.Severity == DiagnosticSeverity.Error);

        var remaining = await Analyze(regenerated.OutputCompilation);
        Assert.DoesNotContain(remaining, diagnosticItem => diagnosticItem.Id == "SPIRE028");

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            regenerated.OutputCompilation,
            "record variant code fix multi-document");
        Assert.NotNull(assembly.GetType("MultiPartUnion+Value"));
    }

    [Fact]
    public async Task FullyAnnotatedRecord_HasNoSerializationDiagnosticOrFix()
    {
        var run = RunUnity(new[] { ("annotated.cs", FullyAnnotatedSource) });
        var diagnostics = await Analyze(run.OutputCompilation);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");

        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "annotated.cs");
        var allDiagnostics = await RunAllRelevantAnalyzers(run.OutputCompilation);
        Assert.DoesNotContain(allDiagnostics, diagnostic => diagnostic.Id == "SPIRE028");
        Assert.Empty(await GetActions(document, null));
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(Compilation compilation)
        => (await RunAllRelevantAnalyzers(compilation))
            .Where(diagnostic => diagnostic.Id == "SPIRE028")
            .ToImmutableArray();

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> RunAllRelevantAnalyzers(Compilation compilation)
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActions(
        Document document,
        Microsoft.CodeAnalysis.Diagnostic? diagnostic)
    {
        if (diagnostic is null)
            return Array.Empty<CodeAction>();

        var actions = new List<CodeAction>();
        await new RecordVariantSerializationCodeFix().RegisterCodeFixesAsync(
            new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None));
        return actions;
    }

    private static AdhocWorkspace CreateWorkspace(Compilation compilation)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RecordVariantCodeFix", "RecordVariantCodeFix", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options);

        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);
        foreach (var tree in compilation.SyntaxTrees)
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                tree.FilePath,
                tree.GetText());

        workspace.TryApplyChanges(solution);
        return workspace;
    }

    private static Solution CreateSolution(Compilation compilation)
    {
        using var workspace = CreateWorkspace(compilation);
        return workspace.CurrentSolution;
    }

    private static Document FindDocument(Solution solution, string fileName)
        => solution.Projects.Single().Documents.Single(document =>
            string.Equals(document.Name, fileName, StringComparison.Ordinal));

    private static UnityGeneratorRun RunUnity(
        IReadOnlyList<(string Path, string Source)> sources)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("UNITY_5_3_OR_NEWER");
        var references = UnityGeneratorTestHarness.GetReferences(
            includeUnityReferences: true,
            includeEditorReferences: false);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(
            source.Source,
            parseOptions,
            source.Path));
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
            parseOptions,
            references);
        var allTrees = compatibilityTree is null
            ? trees.ToArray()
            : trees.Append(compatibilityTree).ToArray();
        var compilation = CSharpCompilation.Create(
            "RecordVariantCodeFix_" + Guid.NewGuid().ToString("N"),
            allTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);
        return new UnityGeneratorRun(
            outputCompilation,
            generatorDiagnostics,
            driver.GetRunResult());
    }

    private const string ComputedPropertySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct ComputedUnion
        {
            [Variant, Serializable]
            public record struct Value(int value)
            {
                public int value => 7;
            }
        }
        """;

    private const string MultiPartPrimarySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct MultiPartUnion
        {
            [Variant, Serializable]
            public partial record struct Value(int value);
        }
        """;

    private const string MultiPartSecondarySource = """
        #nullable enable
        using Houtamelo.Spire;
        using UnityEngine;

        public partial struct MultiPartUnion
        {
            public partial record struct Value
            {
                public int value { get; init; } = value;
            }
        }
        """;

    private const string FullyAnnotatedSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct FullyAnnotatedUnion
        {
            [Variant, Serializable]
            public record struct Value([field: SerializeField] int value);
        }
        """;
}
