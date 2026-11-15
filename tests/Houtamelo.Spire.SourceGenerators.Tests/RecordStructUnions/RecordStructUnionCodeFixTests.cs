using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.RecordStructUnions;

/// Workspace-level contract for the SPIRE_DU020 generator diagnostic and fix.
public sealed class RecordStructUnionCodeFixTests
{
    [Fact]
    public async Task RemoveRecord_UpdatesAllAuthoredPartsAcrossDocumentsAndPreservesNestedPayload()
    {
        var generated = RunGenerator(
            ("state-primary.cs", MultiPartPrimarySource),
            ("state-secondary.cs", MultiPartSecondarySource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        var document = FindDocument(solution, "state-primary.cs");
        var action = Assert.Single(await GetActions(document, diagnostic));

        Assert.Equal("Remove record from union", action.Title);
        Assert.Equal("Spire.Union.RemoveRecord", action.EquivalenceKey);
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());

        var primary = await ReadDocument(apply.ChangedSolution, "state-primary.cs");
        var secondary = await ReadDocument(apply.ChangedSolution, "state-secondary.cs");
        AssertOuterUnionIsStruct(primary, "State");
        AssertOuterUnionIsStruct(secondary, "State");
        Assert.Contains("record struct Value", primary, StringComparison.Ordinal);
        Assert.Contains("record struct Sibling", secondary, StringComparison.Ordinal);

        var regenerated = RunGenerator(
            ("state-primary.cs", primary),
            ("state-secondary.cs", secondary));
        Assert.Empty(regenerated.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(regenerated.Compilation);
    }

    [Fact]
    public async Task RemoveRecord_FixAllUpdatesIndependentUnionsAndRetainsRuntimeEquality()
    {
        var generated = RunGenerator(("fix-all.cs", FixAllSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var duDiagnostics = diagnostics.Where(item => item.Id == "SPIRE_DU020").ToImmutableArray();
        Assert.Equal(2, duDiagnostics.Length);

        var document = FindDocument(solution, "fix-all.cs");
        var provider = new RemoveRecordFromUnionCodeFix();
        var action = Assert.Single(await GetActions(document, duDiagnostics[0], provider));
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            action.EquivalenceKey!,
            provider.FixableDiagnosticIds,
            new FixedDiagnosticProvider(duDiagnostics),
            CancellationToken.None);
        var fixAll = await provider.GetFixAllProvider()!.GetFixAsync(context);
        Assert.NotNull(fixAll);
        var apply = Assert.Single(
            (await fixAll!.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "fix-all.cs");
        AssertOuterUnionIsStruct(changed, "First");
        AssertOuterUnionIsStruct(changed, "Second");

        var regenerated = RunGenerator(("fix-all.cs", changed));
        AssertNoCompilationErrors(regenerated.Compilation);
        using var stream = new MemoryStream();
        Assert.True(regenerated.Compilation.Emit(stream).Success);
        var assembly = LoadAssembly(stream);
        var unionType = assembly.GetType("First", throwOnError: true)!;
        var payloadType = assembly.GetType("First+Value", throwOnError: true)!;
        var leftPayload = Activator.CreateInstance(payloadType, 7)!;
        var rightPayload = Activator.CreateInstance(payloadType, 7)!;
        var conversion = unionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "op_Implicit" &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == payloadType);
        var left = conversion.Invoke(null, new[] { leftPayload })!;
        var right = conversion.Invoke(null, new[] { rightPayload })!;
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task RemoveRecord_PreservesTriviaAndLineEndings(string lineEnding)
    {
        var source = TriviaSource.Replace("\n", lineEnding, StringComparison.Ordinal);
        var generated = RunGenerator(("trivia.cs", source));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        var action = Assert.Single(await GetActions(FindDocument(solution, "trivia.cs"), diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "trivia.cs");

        Assert.Contains("/* before record */", changed, StringComparison.Ordinal);
        Assert.Contains("/* after record */", changed, StringComparison.Ordinal);
        Assert.Contains("// trailing union", changed, StringComparison.Ordinal);
        Assert.Contains(lineEnding, changed, StringComparison.Ordinal);
        AssertOuterUnionIsStruct(changed, "TriviaState");
    }

    [Fact]
    public async Task RemoveRecord_EmptyPrimaryParameterListKeepsCommentAndCompiles()
    {
        var generated = RunGenerator(("empty-parameters.cs", EmptyPrimaryParameterSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        var action = Assert.Single(await GetActions(FindDocument(solution, "empty-parameters.cs"), diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "empty-parameters.cs");

        Assert.Contains("/* keep empty ctor comment */", changed, StringComparison.Ordinal);
        var declaration = CSharpSyntaxTree.ParseText(changed).GetRoot()
            .DescendantNodes().OfType<StructDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == "EmptyState");
        Assert.Contains(declaration.Modifiers, token => token.IsKind(SyntaxKind.PartialKeyword));

        var regenerated = RunGenerator(("empty-parameters.cs", changed));
        AssertNoCompilationErrors(regenerated.Compilation);
    }

    [Fact]
    public async Task RemoveRecord_BodySemicolonKeepsBothCommentsAndCompiles()
    {
        var generated = RunGenerator(("body-semicolon.cs", BodySemicolonSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        var action = Assert.Single(await GetActions(FindDocument(solution, "body-semicolon.cs"), diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changed = await ReadDocument(apply.ChangedSolution, "body-semicolon.cs");

        Assert.Contains("/* keep before semicolon */", changed, StringComparison.Ordinal);
        Assert.Contains("// keep after semicolon", changed, StringComparison.Ordinal);
        AssertOuterUnionIsStruct(changed, "BodyState");

        var regenerated = RunGenerator(("body-semicolon.cs", changed));
        AssertNoCompilationErrors(regenerated.Compilation);
    }

    [Fact]
    public async Task RemoveRecord_FixesMixedStructAndRecordPartialDeclarations()
    {
        var generated = RunGenerator(
            ("mixed-plain.cs", MixedPlainPartSource),
            ("mixed-record.cs", MixedRecordPartSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        var action = Assert.Single(await GetActions(FindDocument(solution, "mixed-record.cs"), diagnostic));
        var apply = Assert.Single(
            (await action.GetOperationsAsync(CancellationToken.None))
                .OfType<ApplyChangesOperation>());
        var changedPlain = await ReadDocument(apply.ChangedSolution, "mixed-plain.cs");
        var changedRecord = await ReadDocument(apply.ChangedSolution, "mixed-record.cs");

        Assert.Contains("public partial struct State", changedPlain, StringComparison.Ordinal);
        AssertOuterUnionIsStruct(changedRecord, "State");
        Assert.Contains("record struct Value", changedRecord, StringComparison.Ordinal);

        var regenerated = RunGenerator(
            ("mixed-plain.cs", changedPlain),
            ("mixed-record.cs", changedRecord));
        AssertNoCompilationErrors(regenerated.Compilation);
    }

    [Fact]
    public async Task RemoveRecord_RefusesMixedPartialDeclarationWithPrimaryParameters()
    {
        var generated = RunGenerator(
            ("mixed-positional-plain.cs", MixedPositionalPlainPartSource),
            ("mixed-positional-record.cs", MixedPositionalRecordPartSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");

        Assert.Empty(await GetActions(
            FindDocument(solution, "mixed-positional-record.cs"),
            diagnostic));
    }

    [Fact]
    public async Task RemoveRecord_RefusesAnyNonEmptyOuterPositionalDeclaration()
    {
        var generated = RunGenerator(("positional.cs", NonEmptyPositionalSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        Assert.Empty(await GetActions(FindDocument(solution, "positional.cs"), diagnostic));
    }

    [Fact]
    public async Task RemoveRecord_RefusesNonEmptyParameterOnAnotherPartialPart()
    {
        var generated = RunGenerator(
            ("partial-positional.cs", PartialPositionalSource));
        var (solution, diagnostics) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "SPIRE_DU020");
        Assert.Empty(await GetActions(FindDocument(solution, "partial-positional.cs"), diagnostic));
    }

    [Fact]
    public async Task RemoveRecord_RefusesOtherDiagnosticsAndStaleLocations()
    {
        var generated = RunGenerator(("negative.cs", BasicSource));
        var (solution, _) = CreateWorkspace(generated.Compilation, generated.Diagnostics);
        var document = FindDocument(solution, "negative.cs");
        var provider = new RemoveRecordFromUnionCodeFix();
        Assert.Equal("SPIRE_DU020", Assert.Single(provider.FixableDiagnosticIds));

        var root = await document.GetSyntaxRootAsync();
        var record = root!.DescendantNodes().OfType<RecordDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == "State");
        var descriptor = new DiagnosticDescriptor(
            "OTHER", "Other", "Other", "Test", DiagnosticSeverity.Error, true);
        var otherDiagnostic = Microsoft.CodeAnalysis.Diagnostic.Create(
            descriptor,
            Location.Create(root.SyntaxTree, record.Keyword.Span));
        Assert.Empty(await GetActions(document, otherDiagnostic, provider));

        var duDescriptor = new DiagnosticDescriptor(
            "SPIRE_DU020", "Struct-based discriminated unions cannot be records",
            "Struct-based discriminated unions cannot be records. Remove the 'record' keyword.",
            "SourceGeneration", DiagnosticSeverity.Error, true);
        var stale = Microsoft.CodeAnalysis.Diagnostic.Create(
            duDescriptor,
            Location.None);
        Assert.Empty(await GetActions(document, stale, provider));

        var foreignTree = CSharpSyntaxTree.ParseText(BasicSource);
        var foreign = Microsoft.CodeAnalysis.Diagnostic.Create(
            duDescriptor,
            foreignTree.GetLocation(record.Keyword.Span));
        Assert.Empty(await GetActions(document, foreign, provider));
    }

    [Fact]
    public async Task RemoveRecord_RefusesUnannotatedLookalikeAndGeneratedDocuments()
    {
        var lookalike = RunPlainCompilation(("lookalike.cs", LookalikeSource));
        var (lookalikeSolution, _) = CreateWorkspace(lookalike, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
        var lookalikeDocument = FindDocument(lookalikeSolution, "lookalike.cs");
        var lookalikeDiagnostic = CreateDiagnosticAtRecord(lookalikeDocument, "FakeState");
        Assert.Empty(await GetActions(lookalikeDocument, lookalikeDiagnostic));

        var generated = RunPlainCompilation(("Generated.g.cs", BasicSource));
        var (generatedSolution, _) = CreateWorkspace(generated, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
        var generatedDocument = FindDocument(generatedSolution, "Generated.g.cs");
        var generatedDiagnostic = CreateDiagnosticAtRecord(generatedDocument, "State");
        Assert.Empty(await GetActions(generatedDocument, generatedDiagnostic));
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActions(
        Document document,
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        CodeFixProvider? provider = null)
    {
        provider ??= new RemoveRecordFromUnionCodeFix();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    private static GeneratedRun RunGenerator(params (string Path, string Source)[] sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Source, parseOptions, source.Path));
        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(typeof(DiscriminatedUnionAttribute).Assembly.Location))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "RecordStructUnionCodeFix_" + Guid.NewGuid().ToString("N"),
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        return new GeneratedRun(outputCompilation, diagnostics, driver.GetRunResult());
    }

    private static Compilation RunPlainCompilation(params (string Path, string Source)[] sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Source, parseOptions, source.Path));
        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(typeof(DiscriminatedUnionAttribute).Assembly.Location));
        return CSharpCompilation.Create(
            "RecordStructUnionCodeFixPlain_" + Guid.NewGuid().ToString("N"),
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static (Solution Solution, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics) CreateWorkspace(
        Compilation compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RecordStructUnionCodeFix", "RecordStructUnionCodeFix", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options);
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var path = string.IsNullOrEmpty(tree.FilePath) ? "source.cs" : tree.FilePath;
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                Path.GetFileName(path),
                tree.GetText(),
                filePath: path);
        }

        var workspaceCompilation = solution.Projects.Single().GetCompilationAsync().GetAwaiter().GetResult()!;
        var remapped = diagnostics.Select(diagnostic =>
        {
            var sourcePath = diagnostic.Location.SourceTree?.FilePath;
            var tree = workspaceCompilation.SyntaxTrees.FirstOrDefault(candidate =>
                string.Equals(candidate.FilePath, sourcePath, StringComparison.Ordinal));
            return tree is null
                ? diagnostic
                : Microsoft.CodeAnalysis.Diagnostic.Create(
                    diagnostic.Descriptor,
                    Location.Create(tree, diagnostic.Location.SourceSpan),
                    diagnostic.Properties);
        }).ToImmutableArray();
        return (solution, remapped);
    }

    private static Document FindDocument(Solution solution, string name)
        => solution.Projects.Single().Documents.Single(document => document.Name == name);

    private static async Task<string> ReadDocument(Solution solution, string name)
        => (await FindDocument(solution, name).GetTextAsync()).ToString();

    private static void AssertOuterUnionIsStruct(string source, string name)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var declaration = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == name);
        Assert.IsType<StructDeclarationSyntax>(declaration);
        Assert.Contains(declaration.Modifiers, token => token.IsKind(SyntaxKind.PartialKeyword));
    }

    private static Microsoft.CodeAnalysis.Diagnostic CreateDiagnosticAtRecord(
        Document document,
        string name)
    {
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult()!;
        var record = root.DescendantNodes().OfType<RecordDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == name);
        var descriptor = new DiagnosticDescriptor(
            "SPIRE_DU020", "Struct-based discriminated unions cannot be records",
            "Struct-based discriminated unions cannot be records. Remove the 'record' keyword.",
            "SourceGeneration", DiagnosticSeverity.Error, true);
        return Microsoft.CodeAnalysis.Diagnostic.Create(
            descriptor,
            record.Keyword.GetLocation());
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    private static Assembly LoadAssembly(MemoryStream stream)
        => Assembly.Load(stream.ToArray());

    private sealed record GeneratedRun(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult);

    private sealed class FixedDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics;

        public FixedDiagnosticProvider(IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
            => this.diagnostics = diagnostics.ToImmutableArray();

        public override async Task<IEnumerable<Microsoft.CodeAnalysis.Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == tree);
        }

        public override Task<IEnumerable<Microsoft.CodeAnalysis.Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<Microsoft.CodeAnalysis.Diagnostic>>(ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);

        public override Task<IEnumerable<Microsoft.CodeAnalysis.Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<Microsoft.CodeAnalysis.Diagnostic>>(diagnostics);
    }

    private const string BasicSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct State
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string MultiPartPrimarySource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct State
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string MultiPartSecondarySource = """
        using Houtamelo.Spire;

        public partial record struct State
        {
            // Keep this sibling declaration untouched.
            [Variant] public record struct Sibling(string text);
        }
        """;

    private const string FixAllSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct First
        {
            [Variant] public record struct Value(int amount);
        }

        [DiscriminatedUnion]
        public partial record struct Second
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string TriviaSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        [System.Serializable]
        public /* before record */ partial /* after record */ record /* before struct */ struct TriviaState // trailing union
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string EmptyPrimaryParameterSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct EmptyState(/* keep empty ctor comment */)
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string BodySemicolonSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct BodyState
        {
            [Variant] public record struct Value(int amount);
        } /* keep before semicolon */; // keep after semicolon
        """;

    private const string MixedPlainPartSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct State
        {
        }
        """;

    private const string MixedRecordPartSource = """
        using Houtamelo.Spire;

        public partial record struct State
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string MixedPositionalPlainPartSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct State
        {
        }
        """;

    private const string MixedPositionalRecordPartSource = """
        using Houtamelo.Spire;

        public partial record struct State(int seed)
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string NonEmptyPositionalSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct State(int seed)
        {
            [Variant] public record struct Value(int amount);
        }
        """;

    private const string PartialPositionalSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record struct State
        {
            [Variant] public record struct Value(int amount);
        }

        public partial record struct State(int seed);
        """;

    private const string LookalikeSource = """
        using System;

        namespace Fake
        {
            [AttributeUsage(AttributeTargets.Struct)]
            public sealed class DiscriminatedUnionAttribute : Attribute { }
        }

        [Fake.DiscriminatedUnion]
        public partial record struct FakeState;
        """;
}
