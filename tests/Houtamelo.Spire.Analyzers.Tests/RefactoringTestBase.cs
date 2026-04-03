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
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.Analyzers.Tests;

/// Base class for CodeRefactoringProvider tests.
/// Uses [| and |] markers in before.cs to define the cursor span.
public abstract class RefactoringTestBase
{
    protected abstract string Category { get; }
    protected abstract ImmutableArray<CodeRefactoringProvider> GetRefactoringProviders();

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Rules.SPIRE016InvalidEnforceInitializationEnumValueAnalyzer).Assembly.Location);

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> CachedReferences =
        new(() => ResolveReferencesAsync());

    [Theory]
    [RefactoringCaseDiscovery]
    public async Task Verify(string caseName)
    {
        if (caseName == RefactoringCaseDiscoveryAttribute.NoCasesSentinel)
            return;

        var casesDir = Path.Combine(AppContext.BaseDirectory, Category, "cases");
        var caseDir = Path.Combine(casesDir, caseName);
        var sharedPath = Path.Combine(casesDir, "_shared.cs");

        var rawBefore = File.ReadAllText(Path.Combine(caseDir, "before.cs"));
        var afterSource = File.ReadAllText(Path.Combine(caseDir, "after.cs"));
        var sharedSource = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : null;

        var (beforeSource, span) = ExtractMarkerSpan(rawBefore);
        if (span == default)
            throw new InvalidOperationException(
                $"Case '{caseName}': before.cs must contain [| and |] markers to define the refactoring span.");

        var references = await CachedReferences.Value;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var beforeTree = CSharpSyntaxTree.ParseText(beforeSource, parseOptions, path: "case.cs");
        var trees = new List<SyntaxTree> { beforeTree };
        if (sharedSource != null)
            trees.Add(CSharpSyntaxTree.ParseText(sharedSource, parseOptions, path: "_shared.cs"));

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestAssembly", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, parseOptions);

        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocId = null;
        foreach (var tree in trees)
        {
            var docId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(docId, Path.GetFileName(tree.FilePath), tree.GetText());
            if (tree.FilePath == "case.cs")
                userDocId = docId;
        }

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply workspace changes");

        if (userDocId is null)
            throw new InvalidOperationException("User document 'case.cs' not found");

        var document = workspace.CurrentSolution.GetDocument(userDocId)!;

        var actions = new List<CodeAction>();
        foreach (var provider in GetRefactoringProviders())
        {
            var context = new CodeRefactoringContext(
                document,
                span,
                action => actions.Add(action),
                CancellationToken.None);

            await provider.ComputeRefactoringsAsync(context);
        }

        if (actions.Count == 0)
            throw new XunitException(
                $"Case '{caseName}': no refactoring actions offered at span [{span.Start}..{span.End}]");

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
                $"Refactoring mismatch for case '{caseName}'.\n\n" +
                $"=== EXPECTED ===\n{afterSource}\n\n" +
                $"=== ACTUAL ===\n{modifiedText}");
        }
    }

    /// Extracts [| and |] markers from source, returns cleaned source and the TextSpan.
    private static (string source, TextSpan span) ExtractMarkerSpan(string raw)
    {
        const string open = "[|";
        const string close = "|]";

        int openIdx = raw.IndexOf(open, StringComparison.Ordinal);
        if (openIdx < 0)
            return (raw, default);

        int closeIdx = raw.IndexOf(close, openIdx + open.Length, StringComparison.Ordinal);
        if (closeIdx < 0)
            return (raw, default);

        var cleaned = raw.Remove(closeIdx, close.Length).Remove(openIdx, open.Length);
        var spanStart = openIdx;
        var spanLength = closeIdx - openIdx - open.Length;

        return (cleaned, new TextSpan(spanStart, spanLength));
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RefactoringCaseDiscoveryAttribute : DataAttribute
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
