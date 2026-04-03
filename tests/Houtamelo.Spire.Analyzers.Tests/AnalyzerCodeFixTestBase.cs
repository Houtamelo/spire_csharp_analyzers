using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.Analyzers.Tests;

/// Base class for analyzer code fix tests (non-generator).
/// Discovers before.cs/after.cs pairs in {Category}/cases/{caseName}/ folders.
public abstract class AnalyzerCodeFixTestBase
{
    protected abstract string Category { get; }
    protected abstract ImmutableArray<DiagnosticAnalyzer> GetAnalyzers();
    protected abstract ImmutableArray<CodeFixProvider> GetCodeFixes();

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Rules.SPIRE016InvalidEnforceInitializationEnumValueAnalyzer).Assembly.Location);

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> CachedReferences =
        new(() => ResolveReferencesAsync());

    [Theory]
    [AnalyzerCodeFixCaseDiscovery]
    public async Task Verify(string caseName)
    {
        if (caseName == AnalyzerCodeFixCaseDiscoveryAttribute.NoCasesSentinel)
            return;

        var casesDir = Path.Combine(AppContext.BaseDirectory, Category, "cases");
        var caseDir = Path.Combine(casesDir, caseName);
        var sharedPath = Path.Combine(casesDir, "_shared.cs");

        var beforeSource = File.ReadAllText(Path.Combine(caseDir, "before.cs"));
        var afterSource = File.ReadAllText(Path.Combine(caseDir, "after.cs"));
        var sharedSource = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : null;

        var references = await CachedReferences.Value;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var beforeTree = CSharpSyntaxTree.ParseText(beforeSource, parseOptions, path: "case.cs");
        var trees = new List<SyntaxTree> { beforeTree };
        if (sharedSource != null)
            trees.Add(CSharpSyntaxTree.ParseText(sharedSource, parseOptions, path: "_shared.cs"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestAssembly", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options)
            .WithProjectParseOptions(projectId, parseOptions);

        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocId = null;
        foreach (var tree in trees)
        {
            var docId = DocumentId.CreateNewId(projectId);
            var fileName = Path.GetFileName(tree.FilePath);
            solution = solution.AddDocument(docId, fileName, tree.GetText());
            if (tree.FilePath == "case.cs")
                userDocId = docId;
        }

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply workspace changes");

        if (userDocId is null)
            throw new InvalidOperationException("User document 'case.cs' not found");

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var wsCompilation = (await project.GetCompilationAsync())!;
        var withAnalyzers = wsCompilation.WithAnalyzers(GetAnalyzers());
        var allDiags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        var fixableIds = GetCodeFixes()
            .SelectMany(p => p.FixableDiagnosticIds)
            .ToHashSet();

        var fixableDiags = allDiags
            .Where(d => fixableIds.Contains(d.Id))
            .Where(d => d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();

        if (fixableDiags.Count == 0)
        {
            var diagSummary = string.Join(", ", allDiags.Select(d => $"{d.Id}@{d.Location}"));
            throw new XunitException(
                $"Case '{caseName}': no fixable diagnostics found. All diagnostics: [{diagSummary}]");
        }

        var diagnostic = fixableDiags[0];
        var provider = GetCodeFixes().First(p => p.FixableDiagnosticIds.Contains(diagnostic.Id));

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

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();
        applyOp.Apply(workspace, CancellationToken.None);

        var modifiedDoc = workspace.CurrentSolution.GetDocument(userDocId)!;
        var modifiedText = (await modifiedDoc.GetTextAsync()).ToString();

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

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class AnalyzerCodeFixCaseDiscoveryAttribute : DataAttribute
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
