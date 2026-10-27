using System;
using System.Collections.Immutable;
using System.Collections.Generic;
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

namespace Houtamelo.Spire.SourceGenerators.Tests.VariantPayloadConsumers;

public sealed class VariantPayloadConsumersTests : CodeFixTestBase
{
    private static readonly string[] AddMissingCases =
    {
        "AddMissingArms_FieldedExpression",
        "AddMissingArms_FieldedStatement",
        "AddMissingArms_FieldlessStatement",
        "AddMissingArms_MultiFieldInlineBraces",
        "AddMissingArms_Generic",
        "AddMissingArms_NestedUnion",
        "AddMissingArms_AcronymAndFactoryConflict",
        "AddMissingArms_KeywordAndNullable",
        "AddMissingArms_UnderscoreParameter",
        "AddMissingArms_ExistingNestedPattern",
        "AddMissingArms_ExistingIsPattern",
    };

    private static readonly string[] IndentationCases =
    {
        "Indentation_AddMissingArms_StatementOutdentedNamespace",
        "Indentation_AddMissingArms_ExpressionOutdentedNamespace",
        "Indentation_AddMissingArms_ExpressionOutdentedNamespaceNoTrailingComma",
        "Indentation_AddMissingArms_EmptyExpressionOutdentedNamespace",
        "Indentation_AddMissingArms_StatementOutdentedNamespaceCRLF",
        "Indentation_AddMissingArms_StatementOutdentedNamespaceTabs",
        "Indentation_AddMissingArms_StatementOutdentedNamespaceTrailingComment",
        "Indentation_AddMissingArms_StatementIndentedNamespace",
        "Indentation_AddMissingArms_ExpressionFileScopedNamespace",
        "Indentation_AddMissingArms_StatementGlobalNamespace",
        "Indentation_AddMissingArms_RecordStatementOutdentedNamespace",
        "Indentation_AddMissingArms_EmptyExpressionOutdentedNamespaceTwoSpaces",
    };

    protected override string Category => "VariantPayloadConsumers";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor(),
            new TypeSafetyAnalyzer(),
            new FieldAccessSafetyAnalyzer());

    protected override ImmutableArray<CodeFixProvider> GetCodeFixes()
        => ImmutableArray.Create<CodeFixProvider>(
            new AddMissingArmsCodeFix(),
            new FixFieldTypeCodeFix());

    public static IEnumerable<object[]> AfterCases()
    {
        foreach (var name in AddMissingCases.Concat(new[]
        {
            "FixFieldType_NestedProperty",
            "FixFieldType_KeywordCapture",
            "FixFieldType_TypedDiscard",
            "FixFieldType_PositionalPayload",
        }))
        {
            yield return new object[] { name };
        }
    }

    public static IEnumerable<object[]> AddMissingCaseData()
    {
        foreach (var name in AddMissingCases)
            yield return new object[] { name };
    }

    public static IEnumerable<object[]> IndentationCaseData()
    {
        foreach (var name in IndentationCases)
            yield return new object[] { name };
    }

    [Theory]
    [MemberData(nameof(AfterCases))]
    public async Task ExpectedAfterSourceCompilesThroughTheGenerator(string caseName)
    {
        var source = File.ReadAllText(GetCasePath(caseName, "after.cs"));
        await AssertGeneratedSourceCompilesAndEmits(
            source,
            caseName,
            assertExhaustiveness: AddMissingCases.Contains(caseName));
    }

    [Theory]
    [MemberData(nameof(AddMissingCaseData))]
    public async Task AppliedAddMissingArmsKeepVariantPatternsInline(string caseName)
    {
        var source = File.ReadAllText(GetCasePath(caseName, "before.cs"));
        var expected = File.ReadAllText(GetCasePath(caseName, "after.cs"));
        var fixedSource = await ApplyFirstFix(source);

        Assert.True(
            GeneratorTestHelper.AreStructurallyEquivalent(fixedSource, expected),
            $"Applied code fix for '{caseName}' did not match after.cs.\n" +
            $"=== EXPECTED ===\n{expected}\n\n=== ACTUAL ===\n{fixedSource}");
        Assert.Contains("Is", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotMatch("case\\s*\\r?\\n\\s*\\{", fixedSource);
        var root = CSharpSyntaxTree.ParseText(fixedSource).GetRoot();
        foreach (var clause in root.DescendantNodes()
                     .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyPatternClauseSyntax>())
        {
            Assert.DoesNotContain("\n", clause.ToFullString());
            Assert.DoesNotContain("\r", clause.ToFullString());
        }

        await AssertGeneratedSourceCompilesAndEmits(
            fixedSource,
            caseName,
            assertExhaustiveness: true);
    }

    [Theory]
    [MemberData(nameof(IndentationCaseData))]
    public async Task AddMissingArmsPreservesExpectedIndentation(string caseName)
    {
        var before = File.ReadAllText(GetIndentationCasePath(caseName, "before.cs"));
        var expected = File.ReadAllText(GetIndentationCasePath(caseName, "after.cs"));
        var usesCrLf = caseName.EndsWith("CRLF", StringComparison.Ordinal);
        var usesTabs = caseName.EndsWith("Tabs", StringComparison.Ordinal);
        var indentationSize = caseName.EndsWith("TwoSpaces", StringComparison.Ordinal)
            ? 2
            : 4;
        if (usesCrLf)
        {
            before = NormalizeLineEndings(before, "\r\n");
            expected = NormalizeLineEndings(expected, "\r\n");
        }

        var actual = await ApplyFirstFix(
            before,
            usesCrLf ? "\r\n" : "\n",
            usesTabs,
            indentationSize);

        Assert.Equal(expected, actual);

        await AssertGeneratedSourceCompilesAndEmits(
            actual,
            caseName,
            assertExhaustiveness: !caseName.EndsWith(
                "RecordStatementOutdentedNamespace",
                StringComparison.Ordinal));
    }

    private static string GetCasePath(string caseName, string fileName)
        => Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "VariantPayloadConsumers",
            "cases",
            caseName,
            fileName);

    private static string GetIndentationCasePath(string caseName, string fileName)
        => Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "ReviewRegressions",
            "cases",
            "AddMissingArmsIndentation",
            caseName,
            fileName);

    private static string NormalizeLineEndings(string text, string lineEnding)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", lineEnding, StringComparison.Ordinal);

    private async Task<string> ApplyFirstFix(
        string source,
        string lineEnding = "\n",
        bool useTabs = false,
        int indentationSize = 4)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        Assert.DoesNotContain(
            generatorDiagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        using var workspace = new AdhocWorkspace();
        var options = workspace.Options
            .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, lineEnding)
            .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, useTabs)
            .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4)
            .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, indentationSize);
        // Configure the options consumed by Document.GetOptionsAsync.
        Assert.True(workspace.TryApplyChanges(workspace.CurrentSolution.WithOptions(options)));
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "VariantPayloadCodeFix", "VariantPayloadCodeFix", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)outputCompilation.SyntaxTrees.First().Options);
        foreach (var reference in outputCompilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocumentId = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, Path.GetFileName(tree.FilePath), tree.GetText());
            if (tree.FilePath.EndsWith("case.cs", StringComparison.Ordinal))
                userDocumentId = documentId;
        }

        Assert.NotNull(userDocumentId);
        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync())!;
        var diagnostics = await compilation.WithAnalyzers(GetAnalyzers()).GetAnalyzerDiagnosticsAsync();
        var fixableIds = GetCodeFixes()
            .SelectMany(provider => provider.FixableDiagnosticIds)
            .ToHashSet(StringComparer.Ordinal);
        var diagnostic = diagnostics.FirstOrDefault(candidate =>
            fixableIds.Contains(candidate.Id) &&
            candidate.Location.SourceTree?.FilePath.EndsWith("case.cs", StringComparison.Ordinal) == true);
        Assert.NotNull(diagnostic);

        var document = solution.GetDocument(userDocumentId!)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic!,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        var provider = GetCodeFixes().First(candidate => candidate.FixableDiagnosticIds.Contains(diagnostic!.Id));
        await provider.RegisterCodeFixesAsync(context);
        Assert.NotEmpty(actions);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        // Inspect the code action's immutable result without depending on workspace application.
        return (await applyOperation.ChangedSolution
            .GetDocument(userDocumentId!)!
            .GetTextAsync()).ToString();
    }

    private async Task AssertGeneratedSourceCompilesAndEmits(
        string source,
        string caseName,
        bool assertExhaustiveness)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        if (assertExhaustiveness)
        {
            var diagnostics = await compilation
                .WithAnalyzers(GetAnalyzers())
                .GetAllDiagnosticsAsync();
            var invalidDiagnostics = diagnostics
                .Where(diagnostic =>
                    diagnostic.Id == "SPIRE009" ||
                    diagnostic.Id == "SPIRE013" ||
                    diagnostic.Id == "SPIRE014" ||
                    (!diagnostic.IsSuppressed &&
                        (diagnostic.Id == "CS8509" || diagnostic.Id == "CS8524")))
                .ToArray();
            Assert.True(
                invalidDiagnostics.Length == 0,
                $"Generated source '{caseName}' still has exhaustiveness diagnostics:\n" +
                string.Join("\n", invalidDiagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
        }

        using var output = new MemoryStream();
        var emit = compilation.Emit(output);
        Assert.True(
            emit.Success,
            string.Join(
                Environment.NewLine,
                emit.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }
}
