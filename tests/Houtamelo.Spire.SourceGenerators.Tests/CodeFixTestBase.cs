using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Sdk;

namespace Spire.SourceGenerators.Tests;

/// Base class for code fix tests. Each test case is a folder with before.cs / after.cs.
/// Pipeline: strip comments → run generator → create workspace → run analyzers →
/// apply code fix → compare result with after.cs via IsEquivalentTo.
public abstract class CodeFixTestBase
{
    protected abstract string Category { get; }
    protected abstract ImmutableArray<DiagnosticAnalyzer> GetAnalyzers();
    protected abstract ImmutableArray<CodeFixProvider> GetCodeFixes();

    [Theory]
    [CodeFixCaseDiscovery]
    public async Task Verify(string caseName)
    {
        if (caseName == CodeFixCaseDiscoveryAttribute.NoCasesSentinel)
            return;

        var casesDir = Path.Combine(AppContext.BaseDirectory, Category, "cases");
        var caseDir = Path.Combine(casesDir, caseName);

        var beforeSource = File.ReadAllText(Path.Combine(caseDir, "before.cs"));
        var afterSource = File.ReadAllText(Path.Combine(caseDir, "after.cs"));

        var stripped = StripComments(beforeSource);

        // Run generator on the stripped user source
        GeneratorTestHelper.RunGenerator(stripped, out var outputCompilation, out _, path: "case.cs");

        // Build a workspace from the output compilation so diagnostics reference workspace documents
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestAssembly", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)outputCompilation.SyntaxTrees.First().Options);

        foreach (var reference in outputCompilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocId = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            var docId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(docId, Path.GetFileName(tree.FilePath), tree.GetText());

            if (tree.FilePath == "case.cs")
                userDocId = docId;
        }

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply workspace changes");

        if (userDocId is null)
            throw new InvalidOperationException("User document 'case.cs' not found in compilation");

        // Run analyzers on the workspace compilation
        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync())!;

        var withAnalyzers = compilation.WithAnalyzers(GetAnalyzers());
        var allDiags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        // Find fixable diagnostics in the user document
        var fixableIds = GetCodeFixes()
            .SelectMany(p => p.FixableDiagnosticIds)
            .ToHashSet();

        var fixableDiags = allDiags
            .Where(d => fixableIds.Contains(d.Id))
            .Where(d => d.Location.SourceTree?.FilePath?.EndsWith("case.cs") == true)
            .ToList();

        if (fixableDiags.Count == 0)
        {
            var diagSummary = string.Join(", ", allDiags.Select(d => $"{d.Id}@{d.Location}"));
            throw new XunitException(
                $"Case '{caseName}': no fixable diagnostics found. All diagnostics: [{diagSummary}]");
        }

        var diagnostic = fixableDiags[0];

        // Find matching code fix provider
        var provider = GetCodeFixes()
            .First(p => p.FixableDiagnosticIds.Contains(diagnostic.Id));

        var document = workspace.CurrentSolution.GetDocument(userDocId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);

        if (actions.Count == 0)
            throw new XunitException(
                $"Case '{caseName}': code fix provider registered no actions for {diagnostic.Id}");

        // Apply first code action
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();
        applyOp.Apply(workspace, CancellationToken.None);

        // Get modified document text
        var modifiedDoc = workspace.CurrentSolution.GetDocument(userDocId)!;
        var modifiedText = (await modifiedDoc.GetTextAsync()).ToString();

        // Compare with expected output (NormalizeWhitespace needed because
        // IsEquivalentTo considers trivia differences in property pattern clauses as structural)
        var actualTree = CSharpSyntaxTree.ParseText(modifiedText);
        var expectedTree = CSharpSyntaxTree.ParseText(afterSource);

        if (!actualTree.GetRoot().NormalizeWhitespace().IsEquivalentTo(
                expectedTree.GetRoot().NormalizeWhitespace()))
        {
            throw new XunitException(
                $"Code fix mismatch for case '{caseName}'.\n\n" +
                $"=== EXPECTED ===\n{afterSource}\n\n" +
                $"=== ACTUAL ===\n{modifiedText}");
        }
    }

    private static string StripComments(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var chars = source.ToCharArray();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                BlankSpan(chars, trivia.Span);
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                     trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                BlankSpan(chars, trivia.FullSpan);
            }
        }

        return new string(chars);
    }

    private static void BlankSpan(char[] chars, Microsoft.CodeAnalysis.Text.TextSpan span)
    {
        for (int i = span.Start; i < span.End && i < chars.Length; i++)
        {
            if (chars[i] != '\n' && chars[i] != '\r')
                chars[i] = ' ';
        }
    }
}

/// Discovers code fix test case folders by scanning for directories containing before.cs.
[AttributeUsage(AttributeTargets.Method)]
public sealed class CodeFixCaseDiscoveryAttribute : DataAttribute
{
    public const string NoCasesSentinel = "__NO_CASES__";

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testClass = testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new InvalidOperationException("Could not determine test class");

        var category = ExtractCategory(testClass.Name);
        var casesDir = Path.Combine(AppContext.BaseDirectory, category, "cases");

        if (!Directory.Exists(casesDir))
        {
            yield return new object[] { NoCasesSentinel };
            yield break;
        }

        bool found = false;
        foreach (var dir in Directory.GetDirectories(casesDir).OrderBy(d => d))
        {
            if (File.Exists(Path.Combine(dir, "before.cs")))
            {
                found = true;
                yield return new object[] { Path.GetFileName(dir) };
            }
        }

        if (!found)
            yield return new object[] { NoCasesSentinel };
    }

    private static string ExtractCategory(string className)
    {
        if (className.EndsWith("Tests", StringComparison.Ordinal))
            return className.Substring(0, className.Length - 5);

        throw new InvalidOperationException(
            $"Test class name '{className}' does not follow the '{{Category}}Tests' convention.");
    }
}
