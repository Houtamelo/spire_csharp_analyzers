using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadonlyRecordVariants;

[Trait("Environment", "Unity")]
public sealed class ReadonlyRecordVariantCodeFixTests
{
    [Fact]
    public async Task RemoveReadonly_AllAuthoredPartialPartsAcrossDocuments()
    {
        var run = RunUnity(
            ("primary.cs", MultiPartPrimarySource),
            ("secondary.cs", MultiPartSecondarySource));
        AssertNoGeneratedErrors(run);

        var diagnostic = Assert.Single(
            await Analyze(run.OutputCompilation),
            item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation);
        var secondary = FindDocument(solution, "secondary.cs");
        var action = Assert.Single(await GetActions(secondary, diagnostic));
        Assert.Equal("Remove readonly from variant", action.Title);

        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changedPrimary = await ReadDocument(apply.ChangedSolution, "primary.cs");
        var changedSecondary = await ReadDocument(apply.ChangedSolution, "secondary.cs");

        Assert.Equal(MultiPartPrimarySource, changedPrimary);
        var secondaryRoot = CSharpSyntaxTree.ParseText(changedSecondary).GetRoot();
        var secondaryValue = secondaryRoot.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == "Value");
        Assert.DoesNotContain(secondaryValue.Modifiers, token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        Assert.Contains("public partial record struct Value", changedSecondary, StringComparison.Ordinal);
        Assert.Contains("public partial record struct Sibling", changedSecondary, StringComparison.Ordinal);

        var regenerated = RunUnity(
            ("primary.cs", changedPrimary),
            ("secondary.cs", changedSecondary));
        AssertNoGeneratedErrors(regenerated);
        Assert.DoesNotContain(await Analyze(regenerated.OutputCompilation), item => item.Id == "SPIRE030");
        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            regenerated.OutputCompilation,
            "readonly record variant multi-document fix");
        Assert.NotNull(assembly.GetType("MultiDocumentUnion+Value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveReadonly_PreservesCommentsDirectivesAndSibling(bool useCrLf)
    {
        var source = useCrLf
            ? TriviaSource.Replace("\n", "\r\n", StringComparison.Ordinal)
            : TriviaSource;
        var run = RunUnity(("trivia.cs", source));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "trivia.cs");
        var action = Assert.Single(await GetActions(document, diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "trivia.cs");

        Assert.Contains("#nullable enable", changed, StringComparison.Ordinal);
        Assert.Contains("#if UNITY_5_3_OR_NEWER", changed, StringComparison.Ordinal);
        Assert.Contains("/* before readonly */", changed, StringComparison.Ordinal);
        Assert.Contains("/* after readonly */", changed, StringComparison.Ordinal);
        Assert.Contains("// trailing declaration comment", changed, StringComparison.Ordinal);
        Assert.Contains("record struct Sibling", changed, StringComparison.Ordinal);
        if (useCrLf)
        {
            Assert.Contains("\r\n", changed, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", changed.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }

        var root = CSharpSyntaxTree.ParseText(
            changed,
            CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.Preview)
                .WithPreprocessorSymbols("UNITY_5_3_OR_NEWER"))
            .GetRoot();
        var value = root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == "Value");
        Assert.DoesNotContain(value.Modifiers, token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        var sibling = root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == "Sibling");
        Assert.DoesNotContain(sibling.Modifiers, token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        Assert.Contains("[Variant, Serializable]", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveReadonly_LeavesGeneratedDocumentsUntouchedWhenFilePathsAreMissing()
    {
        var run = RunUnity(("generated-isolation.cs", BasicReadonlySource));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation, includeFilePaths: false);
        var document = FindDocument(solution, "generated-isolation.cs");
        var generatedBefore = new Dictionary<string, string>();
        foreach (var candidate in solution.Projects.Single().Documents.Where(item => item.Id != document.Id))
            generatedBefore[candidate.Name] = (await candidate.GetTextAsync()).ToString();

        var action = Assert.Single(await GetActions(document, diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changedUser = await ReadDocument(apply.ChangedSolution, "generated-isolation.cs");
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(changedUser).GetRoot()
                .DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Single(record => record.Identifier.ValueText == "Value")
                .Modifiers,
            token => token.IsKind(SyntaxKind.ReadOnlyKeyword));

        foreach (var pair in generatedBefore)
        {
            var changedDocument = apply.ChangedSolution.Projects.Single().Documents.Single(item => item.Name == pair.Key);
            Assert.Equal(pair.Value, (await changedDocument.GetTextAsync()).ToString());
        }

        var regenerated = RunUnity(("generated-isolation.cs", changedUser));
        AssertNoGeneratedErrors(regenerated);
    }

    [Fact]
    public async Task RemoveReadonly_FirstModifierPreservesLeadingTriviaAndCompiles()
    {
        var run = RunUnity(("first-modifier.cs", FirstModifierSource));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "first-modifier.cs");
        var action = Assert.Single(await GetActions(document, diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "first-modifier.cs");

        Assert.Contains("/* keep leading comment */", changed, StringComparison.Ordinal);
        var root = CSharpSyntaxTree.ParseText(changed).GetRoot();
        var value = root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == "Value");
        Assert.DoesNotContain(value.Modifiers, token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        Assert.Contains("public partial record struct Value", changed, StringComparison.Ordinal);

        var regenerated = RunUnity(("first-modifier.cs", changed));
        AssertNoGeneratedErrors(regenerated);
    }

    [Fact]
    public async Task RemoveReadonly_NonPartialLeavesSPIRE028()
    {
        var run = RunUnity(("nonpartial.cs", NonPartialSource));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "nonpartial.cs");
        var action = Assert.Single(await GetActions(document, diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "nonpartial.cs");

        Assert.DoesNotContain("readonly", changed, StringComparison.Ordinal);
        var regenerated = RunUnity(("nonpartial.cs", changed));
        AssertNoGeneratedErrors(regenerated);
        var remaining = await Analyze(regenerated.OutputCompilation);
        Assert.DoesNotContain(remaining, item => item.Id == "SPIRE030");
        Assert.Contains(remaining, item => item.Id == "SPIRE028");
    }

    [Fact]
    public async Task RemoveReadonly_PreservesSPIRE029()
    {
        var run = RunUnity(("reconstruction.cs", ReconstructionSource));
        AssertNoGeneratedErrors(run);
        var diagnostics = await Analyze(run.OutputCompilation);
        var readonlyDiagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE030");
        Assert.Contains(diagnostics, item => item.Id == "SPIRE029");

        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "reconstruction.cs");
        var action = Assert.Single(await GetActions(document, readonlyDiagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "reconstruction.cs");
        var regenerated = RunUnity(("reconstruction.cs", changed));
        AssertNoGeneratedErrors(regenerated);
        var remaining = await Analyze(regenerated.OutputCompilation);
        Assert.DoesNotContain(remaining, item => item.Id == "SPIRE030");
        Assert.Contains(remaining, item => item.Id == "SPIRE029");
    }

    [Fact]
    public async Task RemoveReadonly_FixAllUpdatesBothVariants()
    {
        var run = RunUnity(("fix-all.cs", FixAllSource));
        AssertNoGeneratedErrors(run);
        var solution = CreateSolution(run.OutputCompilation);
        var workspaceCompilation = (await solution.Projects.Single().GetCompilationAsync())!;
        var diagnostics = (await Analyze(workspaceCompilation))
            .Where(item => item.Id == "SPIRE030")
            .ToImmutableArray();
        Assert.Equal(2, diagnostics.Length);

        var document = FindDocument(solution, "fix-all.cs");
        var provider = new ReadonlyRecordVariantSerializationCodeFix();
        var actions = await GetActions(document, diagnostics[0], provider);
        var action = Assert.Single(actions);
        Assert.Equal("Spire.RecordVariant.RemoveReadonly", action.EquivalenceKey);
        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            action.EquivalenceKey!,
            provider.FixableDiagnosticIds,
            new FixedDiagnosticProvider(diagnostics),
            CancellationToken.None);
        var fixAllAction = await fixAll!.GetFixAsync(context);
        Assert.NotNull(fixAllAction);
        var apply = Assert.Single(
            (await fixAllAction!.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "fix-all.cs");
        var root = CSharpSyntaxTree.ParseText(changed).GetRoot();
        Assert.DoesNotContain(
            root.DescendantNodes().OfType<RecordDeclarationSyntax>(),
            record => (record.Identifier.ValueText is "First" or "Second") &&
                record.Modifiers.Any(token => token.IsKind(SyntaxKind.ReadOnlyKeyword)));

        var regenerated = RunUnity(("fix-all.cs", changed));
        AssertNoGeneratedErrors(regenerated);
        Assert.DoesNotContain(await Analyze(regenerated.OutputCompilation), item => item.Id == "SPIRE030");
    }

    [Fact]
    public async Task ProviderIgnoresSPIRE028AndHasOnlySPIRE030FixableId()
    {
        var run = RunUnity(("other-diagnostic.cs", MutableMissingMetadataSource));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE028");
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "other-diagnostic.cs");
        var provider = new ReadonlyRecordVariantSerializationCodeFix();
        Assert.Equal("SPIRE030", Assert.Single(provider.FixableDiagnosticIds));
        Assert.Empty(await GetActions(document, diagnostic, provider));
    }

    [Fact]
    public async Task FieldlessReadonlyVariantProducesNoDiagnosticOrAction()
    {
        var run = RunUnity(("fieldless.cs", FieldlessSource));
        AssertNoGeneratedErrors(run);
        var diagnostics = await Analyze(run.OutputCompilation);
        Assert.DoesNotContain(diagnostics, item => item.Id == "SPIRE030");
        Assert.DoesNotContain(diagnostics, item => item.Id == "SPIRE029");

        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "fieldless.cs");
        var synthetic = CreateSyntheticDiagnostic(run.OutputCompilation, "fieldless.cs", "Empty");
        Assert.Empty(await GetActions(document, synthetic));
    }

    [Fact]
    public async Task ProviderDeclinesStaleDiagnosticSpan()
    {
        var run = RunUnity(("stale.cs", BasicReadonlySource));
        AssertNoGeneratedErrors(run);
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "stale.cs");
        var synthetic = CreateSyntheticDiagnostic(run.OutputCompilation, "stale.cs", marker: null);
        Assert.Empty(await GetActions(document, synthetic));
    }

    [Fact]
    public async Task ProviderDeclinesMutableNonVariantAndLookalikeTargets()
    {
        var cases = new[]
        {
            (Path: "mutable-target.cs", Source: MutableTargetSource, Marker: "Value"),
            (Path: "nonvariant-target.cs", Source: NonVariantTargetSource, Marker: "Metadata"),
            (Path: "lookalike-target.cs", Source: LookalikeTargetSource, Marker: "Metadata")
        };

        foreach (var testCase in cases)
        {
            var run = RunUnity((testCase.Path, testCase.Source));
            UnityGeneratorTestHarness.AssertNoCompilationErrors(run);
            var solution = CreateSolution(run.OutputCompilation);
            var document = FindDocument(solution, testCase.Path);
            var synthetic = CreateSyntheticDiagnostic(run.OutputCompilation, testCase.Path, testCase.Marker);
            Assert.Empty(await GetActions(document, synthetic));
        }
    }

    [Fact]
    public async Task ProviderDeclinesGeneratedOnlyTarget()
    {
        var run = RunUnity(("generated-target.cs", BasicReadonlySource));
        AssertNoGeneratedErrors(run);
        var generatedTree = run.RunResult.GeneratedTrees.First(tree =>
            tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal));
        var solution = CreateSolution(run.OutputCompilation);
        var generatedDocument = solution.Projects.Single().Documents.Single(document =>
            string.Equals(document.FilePath, generatedTree.FilePath, StringComparison.Ordinal));
        var synthetic = CreateSyntheticDiagnostic(run.OutputCompilation, generatedTree.FilePath, marker: null);
        Assert.Empty(await GetActions(generatedDocument, synthetic));
    }

    [Fact]
    public async Task ProviderDeclinesForeignEmptyPathDiagnostic()
    {
        var run = RunUnity(("current.cs", BasicReadonlySource));
        AssertNoGeneratedErrors(run);
        var solution = CreateSolution(run.OutputCompilation);
        var document = FindDocument(solution, "current.cs");
        var foreignSource = BasicReadonlySource.Replace("Value", "Other", StringComparison.Ordinal);
        var foreignTree = CSharpSyntaxTree.ParseText(foreignSource);
        var readonlyStart = foreignSource.IndexOf("readonly", StringComparison.Ordinal);
        var descriptor = new RecordVariantDeclarationAnalyzer().SupportedDiagnostics
            .Single(item => item.Id == "SPIRE030");
        var diagnostic = RoslynDiagnostic.Create(
            descriptor,
            foreignTree.GetLocation(new TextSpan(readonlyStart, "readonly".Length)),
            "Other",
            "BasicReadonlyUnion");

        Assert.Empty(await GetActions(document, diagnostic));
    }

    [Fact]
    public async Task RemoveReadonly_AllReadonlyPartialPartsAcrossDocuments()
    {
        var run = RunUnity(
            ("both-primary.cs", BothReadonlyPrimarySource),
            ("both-secondary.cs", BothReadonlySecondarySource));
        AssertNoGeneratedErrors(run);
        var diagnostic = Assert.Single(await Analyze(run.OutputCompilation), item => item.Id == "SPIRE030");
        var solution = CreateSolution(run.OutputCompilation);
        var diagnosticDocument = solution.Projects.Single().Documents.Single(document =>
            string.Equals(document.FilePath, diagnostic.Location.SourceTree?.FilePath, StringComparison.Ordinal));
        var action = Assert.Single(await GetActions(diagnosticDocument, diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changedPrimary = await ReadDocument(apply.ChangedSolution, "both-primary.cs");
        var changedSecondary = await ReadDocument(apply.ChangedSolution, "both-secondary.cs");
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(changedPrimary).GetRoot().DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Single(record => record.Identifier.ValueText == "Value")
                .Modifiers,
            token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(changedSecondary).GetRoot().DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Single(record => record.Identifier.ValueText == "Value")
                .Modifiers,
            token => token.IsKind(SyntaxKind.ReadOnlyKeyword));
        Assert.Contains("Sibling", changedSecondary, StringComparison.Ordinal);

        var regenerated = RunUnity(
            ("both-primary.cs", changedPrimary),
            ("both-secondary.cs", changedSecondary));
        AssertNoGeneratedErrors(regenerated);
        Assert.DoesNotContain(await Analyze(regenerated.OutputCompilation), item => item.Id == "SPIRE030");
    }

    private static async Task<ImmutableArray<RoslynDiagnostic>> Analyze(Compilation compilation)
        => (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .Where(item => item.Id is "SPIRE028" or "SPIRE029" or "SPIRE030")
            .ToImmutableArray();

    private static async Task<IReadOnlyList<CodeAction>> GetActions(
        Document document,
        RoslynDiagnostic diagnostic,
        CodeFixProvider? provider = null)
    {
        provider ??= new ReadonlyRecordVariantSerializationCodeFix();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    private static RoslynDiagnostic CreateSyntheticDiagnostic(
        Compilation compilation,
        string fileName,
        string? marker)
    {
        var tree = compilation.SyntaxTrees.First(candidate =>
            string.Equals(candidate.FilePath, fileName, StringComparison.Ordinal) ||
            string.Equals(Path.GetFileName(candidate.FilePath), Path.GetFileName(fileName), StringComparison.Ordinal));
        var text = tree.GetText().ToString();
        var start = marker is null ? 0 : text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            start = 0;
        var span = new TextSpan(start, marker is null ? 0 : marker.Length);
        var descriptor = new RecordVariantDeclarationAnalyzer().SupportedDiagnostics
            .Single(item => item.Id == "SPIRE030");
        return RoslynDiagnostic.Create(
            descriptor,
            tree.GetLocation(span),
            "SyntheticVariant",
            "SyntheticUnion");
    }

    private static void AssertNoGeneratedErrors(UnityGeneratorRun run)
    {
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(run);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(run);
    }

    private static Solution CreateSolution(Compilation compilation, bool includeFilePaths = true)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ReadonlyRecordVariantCodeFix", "ReadonlyRecordVariantCodeFix", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options);
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var path = string.IsNullOrEmpty(tree.FilePath) ? "generated.cs" : tree.FilePath;
            var documentId = DocumentId.CreateNewId(projectId);
            solution = includeFilePaths
                ? solution.AddDocument(documentId, Path.GetFileName(path), tree.GetText(), filePath: path)
                : solution.AddDocument(documentId, Path.GetFileName(path), tree.GetText());
        }
        return solution;
    }

    private static Document FindDocument(Solution solution, string fileName)
        => solution.Projects.Single().Documents.Single(document =>
            string.Equals(document.Name, fileName, StringComparison.Ordinal));

    private static async Task<string> ReadDocument(Solution solution, string fileName)
        => (await FindDocument(solution, fileName).GetTextAsync()).ToString();

    private static UnityGeneratorRun RunUnity(params (string Path, string Source)[] sources)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("UNITY_5_3_OR_NEWER");
        var references = UnityGeneratorTestHarness.GetReferences(true, false);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(
            source.Source,
            parseOptions,
            source.Path));
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(parseOptions, references);
        var allTrees = compatibilityTree is null
            ? trees.ToArray()
            : trees.Append(compatibilityTree).ToArray();
        var compilation = CSharpCompilation.Create(
            "ReadonlyRecordVariantCodeFix_" + Guid.NewGuid().ToString("N"),
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

    private sealed class FixedDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<RoslynDiagnostic> diagnostics;

        public FixedDiagnosticProvider(IEnumerable<RoslynDiagnostic> diagnostics)
            => this.diagnostics = diagnostics.ToImmutableArray();

        public override async Task<IEnumerable<RoslynDiagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            var path = tree?.FilePath;
            return diagnostics.Where(item => item.Location.SourceTree?.FilePath == path);
        }

        public override Task<IEnumerable<RoslynDiagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoslynDiagnostic>>(ImmutableArray<RoslynDiagnostic>.Empty);

        public override Task<IEnumerable<RoslynDiagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoslynDiagnostic>>(diagnostics);
    }

    private const string MultiPartPrimarySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct MultiDocumentUnion
        {
            [Variant, Serializable]
            public partial record struct Value(int amount);
            [Variant, Serializable]
            public partial record struct Sibling(int sibling);
        }
        """;

    private const string MultiPartSecondarySource = """
        #nullable enable
        using Houtamelo.Spire;

        public partial struct MultiDocumentUnion
        {
            // The readonly modifier is intentionally authored in another document.
            public readonly partial record struct Value;
            public partial record struct Sibling;
        }
        """;

    private const string TriviaSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct TriviaUnion
        {
            #if UNITY_5_3_OR_NEWER
            [Variant, Serializable]
            public /* before readonly */ readonly /* after readonly */ partial record struct Value(int amount); // trailing declaration comment
            #endif
            [Variant, Serializable]
            public partial record struct Sibling(int sibling);
        }
        """;

    private const string NonPartialSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct NonPartialUnion
        {
            [Variant]
            public readonly record struct Value(int amount);
        }
        """;

    private const string ReconstructionSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct ReconstructionUnion
        {
            [Variant, Serializable]
            public readonly partial record struct Value([field: SerializeField] int amount)
            {
                public int amount { get; init; } = amount + 1;
            }
        }
        """;

    private const string FixAllSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct FixAllUnion
        {
            [Variant, Serializable]
            public readonly partial record struct First(int first);
            [Variant, Serializable]
            public readonly partial record struct Second(int second);
        }
        """;

    private const string MutableMissingMetadataSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct MutableMetadataUnion
        {
            [Variant]
            public record struct Value(int amount);
        }
        """;

    private const string FieldlessSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct FieldlessUnion
        {
            [Variant, Serializable]
            public readonly partial record struct Empty;
        }
        """;

    private const string BasicReadonlySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct BasicReadonlyUnion
        {
            [Variant, Serializable]
            public readonly partial record struct Value(int amount);
        }
        """;

    private const string FirstModifierSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct FirstModifierUnion
        {
            [Variant, Serializable]
            readonly /* keep leading comment */ public partial record struct Value(int amount);
        }
        """;

    private const string BothReadonlyPrimarySource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct BothReadonlyUnion
        {
            [Variant, Serializable]
            public readonly partial record struct Value(int amount);
            [Variant, Serializable]
            public partial record struct Sibling(int sibling);
        }
        """;

    private const string BothReadonlySecondarySource = """
        #nullable enable
        using Houtamelo.Spire;

        public partial struct BothReadonlyUnion
        {
            public readonly partial record struct Value;
            public partial record struct Sibling;
        }
        """;

    private const string MutableTargetSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct MutableTargetUnion
        {
            [Variant, Serializable]
            public partial record struct Value([field: SerializeField] int amount);
        }
        """;

    private const string NonVariantTargetSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct NonVariantTargetUnion
        {
            [Variant, Serializable]
            public partial record struct Other([field: SerializeField] int amount);
            public readonly partial record struct Metadata(int amount);
        }
        """;

    private const string LookalikeTargetSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        namespace Lookalike
        {
            [AttributeUsage(AttributeTargets.Struct)]
            public sealed class VariantAttribute : Attribute { }
        }

        [Serializable, DiscriminatedUnion]
        public partial struct LookalikeTargetUnion
        {
            [Variant, Serializable]
            public partial record struct Other([field: SerializeField] int amount);
            [Lookalike.Variant]
            public readonly partial record struct Metadata(int amount);
        }
        """;
}
