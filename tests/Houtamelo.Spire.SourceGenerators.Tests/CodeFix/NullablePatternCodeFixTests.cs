using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

/// Exercises the nullable-pattern code fixes through a real generator,
/// workspace, analyzer, code action/refactoring, compilation, and emit.
public sealed class NullablePatternCodeFixTests
{
    private const string CasesDirectory = "NullablePattern";
    private const string ThrowExpression = "throw new System.NotImplementedException()";

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        ImmutableArray.Create<DiagnosticAnalyzer>(
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor(),
            new TypeSafetyAnalyzer());

    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "AddMissingArms_StructSharedArityNullableReference_Expression" };
        yield return new object[] { "AddMissingArms_StructUniqueArityNullableReference_Statement" };
        yield return new object[] { "AddMissingArms_StructNullableArrayAndValue_Expression" };
        yield return new object[] { "AddMissingArms_StructNonNullableControl_Statement" };
        yield return new object[] { "AddMissingArms_StructNullableDisabledReference_Expression" };
        yield return new object[] { "AddMissingArms_StructCompatibleNullableMerge_Expression" };
        yield return new object[] { "AddMissingArms_StructIncompatibleFieldCollision_Expression" };
        yield return new object[] { "AddMissingArms_StructAcronymCollision_Expression" };
        yield return new object[] { "AddMissingArms_StructKeywordCapture_Expression" };
        yield return new object[] { "AddMissingArms_StructEnclosingLocalCollision_Expression" };
        yield return new object[] { "AddMissingArms_StructTupleElementNameCollision_Expression" };
        yield return new object[] { "AddMissingArms_StructEnclosingPatternCollision_Expression" };
        yield return new object[] { "ExpandWildcard_StructSharedArityNullableReference_Expression" };
        yield return new object[] { "ExpandWildcard_StructNullableValue_Expression" };
        yield return new object[] { "ExpandWildcard_StructNullableArray_Expression" };
        yield return new object[] { "ExpandWildcard_StructPropertyCoveredVariant_Expression" };
        yield return new object[] { "AddMissingArms_StructUnderscoreParameter_Expression" };
        yield return new object[] { "AddMissingArms_StructTrailingLocalCollision_Expression" };
        yield return new object[] { "AddMissingArms_GenericStructKindPrefix_Expression" };
        yield return new object[] { "AddMissingArms_NestedGenericContainingKindPrefix_Expression" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NullablePatterns_AreGeneratedAndExecutable(string caseName)
    {
        var caseDirectory = GetCaseDirectory(caseName);
        var beforeSource = File.ReadAllText(Path.Combine(caseDirectory, "before.cs"));
        var expectedSource = File.ReadAllText(Path.Combine(caseDirectory, "after.cs"));

        var isWildcard = caseName.StartsWith("ExpandWildcard_", StringComparison.Ordinal);
        var fixedSource = isWildcard
            ? await ApplyWildcardRefactoring(caseName, beforeSource)
            : await ApplyAddMissingArms(caseName, beforeSource);

        Assert.True(
            AreStructurallyEquivalent(fixedSource, expectedSource),
            $"Code action output for '{caseName}' did not match the expected source.\n\n" +
            $"=== EXPECTED ===\n{expectedSource}\n\n=== ACTUAL ===\n{fixedSource}");

        using (var fixedWorkspace = await CreateWorkspace(fixedSource))
        {
            var fixedCompilation = await GetCompilation(fixedWorkspace);
            AssertNoCompilationErrors(fixedCompilation, caseName);
            await AssertNoPatternDiagnostics(fixedCompilation, caseName);
        }

        if (isWildcard)
        {
            var wildcardRuntimeSource = ReplaceWildcardArmBody(caseName, fixedSource);
            var assembly = CompileAndLoad(wildcardRuntimeSource, caseName + " runtime");
            Assert.Equal("null", InvokeProbe(assembly, "NullInput"));
            Assert.Equal("value", InvokeProbe(assembly, "ValueInput"));
            return;
        }

        var runtimeSource = ReplaceGeneratedArmBody(caseName, fixedSource);
        var runtimeAssembly = CompileAndLoad(runtimeSource, caseName + " runtime");
        if (RequiresOnlyNonNullInput(caseName))
        {
            Assert.Equal(
                ExpectedRuntimeResult(caseName, isNull: false),
                InvokeProbe(runtimeAssembly, "ValueInput"));
        }
        else
        {
            Assert.Equal(
                ExpectedRuntimeResult(caseName, isNull: true),
                InvokeProbe(runtimeAssembly, "NullInput"));
            Assert.Equal(
                ExpectedRuntimeResult(caseName, isNull: false),
                InvokeProbe(runtimeAssembly, "ValueInput"));
        }
    }

    private static string GetCaseDirectory(string caseName)
        => Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "CodeFix",
            "cases",
            CasesDirectory,
            caseName);

    private static async Task<string> ApplyAddMissingArms(
        string caseName,
        string source)
    {
        using var state = await CreateWorkspace(source);
        var compilation = await GetCompilation(state);
        var diagnostics = await GetAnalyzerDiagnostics(compilation);
        var diagnostic = diagnostics.FirstOrDefault(d =>
            d.Id == "SPIRE009" &&
            d.Location.SourceTree?.FilePath?.EndsWith("case.cs", StringComparison.Ordinal) == true);

        Assert.NotNull(diagnostic);

        var document = state.Workspace.CurrentSolution.GetDocument(state.UserDocumentId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic!,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await new AddMissingArmsCodeFix().RegisterCodeFixesAsync(context);
        Assert.NotEmpty(actions);

        await ApplyAction(state.Workspace, actions[0]);
        return await GetUserSource(state);
    }

    private static async Task<string> ApplyWildcardRefactoring(
        string caseName,
        string source)
    {
        using var state = await CreateWorkspace(source);
        var document = state.Workspace.CurrentSolution.GetDocument(state.UserDocumentId)!;
        var root = await document.GetSyntaxRootAsync();
        Assert.NotNull(root);

        var wildcards = root!.DescendantNodes()
            .OfType<DiscardPatternSyntax>()
            .Where(pattern => pattern.Parent is SwitchExpressionArmSyntax)
            .ToList();
        Assert.Single(wildcards);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            wildcards[0].Span,
            action => actions.Add(action),
            CancellationToken.None);

        await new ExpandWildcardRefactoring().ComputeRefactoringsAsync(context);
        Assert.NotEmpty(actions);

        await ApplyAction(state.Workspace, actions[0]);
        return await GetUserSource(state);
    }

    private static async Task ApplyAction(Workspace workspace, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        applyOperation.Apply(workspace, CancellationToken.None);
    }

    private static async Task<WorkspaceState> CreateWorkspace(string source)
    {
        var outputCompilation = RunGenerator(source, out var generatorDiagnostics);

        var generatorErrors = generatorDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(generatorErrors);

        var userTree = outputCompilation.SyntaxTrees
            .First(tree => tree.FilePath.EndsWith("case.cs", StringComparison.Ordinal));
        var parseOptions = (CSharpParseOptions)userTree.Options;
        var projectId = ProjectId.CreateNewId();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "NullablePatternCodeFix", "NullablePatternCodeFix", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(projectId, parseOptions);

        foreach (var reference in outputCompilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocumentId = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(
                documentId,
                Path.GetFileName(tree.FilePath),
                tree.GetText());

            if (tree.FilePath.EndsWith("case.cs", StringComparison.Ordinal))
                userDocumentId = documentId;
        }

        if (!workspace.TryApplyChanges(solution))
        {
            workspace.Dispose();
            throw new InvalidOperationException("Failed to apply the code-fix workspace changes.");
        }

        if (userDocumentId is null)
        {
            workspace.Dispose();
            throw new InvalidOperationException("Generated workspace does not contain case.cs.");
        }

        return new WorkspaceState(workspace, projectId, userDocumentId);
    }

    private static async Task<Compilation> GetCompilation(WorkspaceState state)
    {
        var project = state.Workspace.CurrentSolution.GetProject(state.ProjectId)!;
        return (await project.GetCompilationAsync())!;
    }

    private static async Task<string> GetUserSource(WorkspaceState state)
    {
        var document = state.Workspace.CurrentSolution.GetDocument(state.UserDocumentId)!;
        return (await document.GetTextAsync()).ToString();
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> GetAnalyzerDiagnostics(
        Compilation compilation)
    {
        var withAnalyzers = compilation.WithAnalyzers(Analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task AssertNoPatternDiagnostics(
        Compilation compilation,
        string caseName)
    {
        var diagnostics = await GetAnalyzerDiagnostics(compilation);
        var remaining = diagnostics
            .Where(diagnostic =>
                (diagnostic.Id == "SPIRE009" || diagnostic.Id == "SPIRE011") &&
                diagnostic.Location.SourceTree?.FilePath?.EndsWith(
                    "case.cs",
                    StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(
            remaining.Count == 0,
            $"Pattern diagnostics remained after applying '{caseName}':\n" +
            string.Join("\n", remaining.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static void AssertNoCompilationErrors(Compilation compilation, string caseName)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            $"Compilation errors remained after applying '{caseName}':\n" +
            string.Join("\n", errors.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static Assembly CompileAndLoad(string source, string description)
    {
        var compilation = RunGenerator(
            source,
            out var generatorDiagnostics,
            runtimeCompilation: true);

        var generatorErrors = generatorDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(
            generatorErrors.Count == 0,
            $"Generator errors while compiling '{description}':\n" +
            string.Join("\n", generatorErrors.Select(diagnostic => diagnostic.GetMessage())));

        AssertNoCompilationErrors(compilation, description);

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.True(
            emitResult.Success,
            $"Emit failed for '{description}':\n" +
            string.Join("\n", emitResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")));

        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private static Compilation RunGenerator(
        string source,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> generatorDiagnostics,
        bool runtimeCompilation = false)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out generatorDiagnostics,
            path: "case.cs");

#if SPIRE_UNITY_TEST_HOST
        if (runtimeCompilation)
        {
            var runtimeReferences = TestHarnessEnvironment.RuntimeReferences;
            var coreAssembly = MetadataReference.CreateFromFile(
                typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);
            if (!runtimeReferences.Any(reference =>
                    string.Equals(reference.Display, coreAssembly.Display, StringComparison.OrdinalIgnoreCase)))
            {
                runtimeReferences = runtimeReferences.Add(coreAssembly);
            }

            outputCompilation = outputCompilation.WithReferences(runtimeReferences);
        }
#endif

        var userTree = outputCompilation.SyntaxTrees
            .First(tree => tree.FilePath.EndsWith("case.cs", StringComparison.Ordinal));
        var compatibilityTree = CreateRuntimeCompatibilityTree(
            (CSharpParseOptions)userTree.Options,
            outputCompilation.References);
        if (compatibilityTree is not null)
            outputCompilation = outputCompilation.AddSyntaxTrees(compatibilityTree);

        return outputCompilation.WithAssemblyName(
            $"NullablePattern_{Guid.NewGuid():N}");
    }

    private static SyntaxTree? CreateRuntimeCompatibilityTree(
        CSharpParseOptions parseOptions,
        IEnumerable<MetadataReference> references)
    {
#if SPIRE_UNITY_TEST_HOST
        return CSharpSyntaxTree.ParseText(
            "namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }",
            parseOptions,
            path: "__SpireUnityIsExternalInit.cs");
#else
        return TestHarnessEnvironment.CreateRuntimeCompatibilityTree(parseOptions, references);
#endif
    }

    private static string InvokeProbe(Assembly assembly, string inputMethodName)
    {
        var probeType = assembly.GetType("TestNs.Probe", throwOnError: true)!;
        var inputMethod = probeType.GetMethod(
            inputMethodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(inputMethod);

        var command = inputMethod!.Invoke(null, Array.Empty<object>());
        var evaluateMethod = probeType.GetMethod(
            "Evaluate",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(evaluateMethod);

        return (string)evaluateMethod!.Invoke(null, new[] { command })!;
    }

    private static string ReplaceGeneratedArmBody(string caseName, string source)
    {
        Assert.Contains(ThrowExpression, source);

        var replacement = caseName.Contains(
            "NullableArrayAndValue",
            StringComparison.Ordinal)
            ? "Probe.CaptureBatch(items, count)"
            : caseName.Contains(
                "FieldCollision",
                StringComparison.Ordinal) || caseName.Contains(
                "AcronymCollision",
                StringComparison.Ordinal)
                ? "Probe.CaptureText(value)"
            : caseName.Contains(
                "KeywordCapture",
                StringComparison.Ordinal)
                ? "Probe.CaptureEvent(@event)"
            : caseName.Contains(
                "EnclosingLocalCollision",
                StringComparison.Ordinal)
                ? "Probe.CaptureInt(value1)"
            : caseName.Contains(
                "TupleElementNameCollision",
                StringComparison.Ordinal)
                ? "Probe.CaptureValue(value)"
            : caseName.Contains(
                "EnclosingPatternCollision",
                StringComparison.Ordinal)
                ? "Probe.CaptureInt(value1)"
            : caseName.Contains(
                "UnderscoreParameter",
                StringComparison.Ordinal)
                ? "Probe.CaptureInt(_1)"
            : caseName.Contains(
                "TrailingLocalCollision",
                StringComparison.Ordinal)
                ? "Probe.CaptureInt(value1)"
            : caseName.Contains(
                "GenericStruct",
                StringComparison.Ordinal) || caseName.Contains(
                "NestedGenericContaining",
                StringComparison.Ordinal)
                ? "Probe.CaptureInt(value)"
            : caseName.Contains(
                "Statement",
                StringComparison.Ordinal)
                ? "return Probe.CaptureEffect(effect)"
                : "Probe.CaptureEffect(effect)";

        return source.Replace(ThrowExpression, replacement);
    }

    private static bool RequiresOnlyNonNullInput(string caseName)
        => caseName.Contains("NonNullableControl", StringComparison.Ordinal)
            || caseName.Contains("FieldCollision", StringComparison.Ordinal)
            || caseName.Contains("AcronymCollision", StringComparison.Ordinal)
            || caseName.Contains("KeywordCapture", StringComparison.Ordinal)
            || caseName.Contains("EnclosingLocalCollision", StringComparison.Ordinal)
            || caseName.Contains("TupleElementNameCollision", StringComparison.Ordinal)
            || caseName.Contains("EnclosingPatternCollision", StringComparison.Ordinal)
            || caseName.Contains("UnderscoreParameter", StringComparison.Ordinal)
            || caseName.Contains("TrailingLocalCollision", StringComparison.Ordinal)
            || caseName.Contains("GenericStruct", StringComparison.Ordinal)
            || caseName.Contains("NestedGenericContaining", StringComparison.Ordinal);

    private static string ExpectedRuntimeResult(string caseName, bool isNull)
    {
        if (caseName.Contains("NullableArrayAndValue", StringComparison.Ordinal))
            return isNull ? "null:null" : "1:3";

        if (caseName.Contains("IncompatibleFieldCollision", StringComparison.Ordinal))
            return "text";

        if (caseName.Contains("AcronymCollision", StringComparison.Ordinal))
            return "url";

        if (caseName.Contains("KeywordCapture", StringComparison.Ordinal))
            return "event";

        if (caseName.Contains("UnderscoreParameter", StringComparison.Ordinal))
            return "42";

        if (caseName.Contains("GenericStruct", StringComparison.Ordinal) ||
            caseName.Contains("NestedGenericContaining", StringComparison.Ordinal))
            return "42";

        if (caseName.Contains("EnclosingLocalCollision", StringComparison.Ordinal) ||
            caseName.Contains("EnclosingPatternCollision", StringComparison.Ordinal) ||
            caseName.Contains("TrailingLocalCollision", StringComparison.Ordinal))
            return "42";

        return isNull ? "null" : "value";
    }

    private static string ReplaceWildcardArmBody(string caseName, string source)
    {
        const string wildcardBody = "\"wildcard\"";
        Assert.Equal(1, source.Split(wildcardBody).Length - 1);

        var replacement = caseName.Contains(
            "SharedArity",
            StringComparison.Ordinal)
            ? "Probe.CaptureEffect(effect)"
            : caseName.Contains(
                "PropertyCoveredVariant",
                StringComparison.Ordinal)
                ? "Probe.CaptureEffect(effect)"
            : caseName.Contains(
                "NullableValue",
                StringComparison.Ordinal)
                ? "Probe.CaptureCount(count)"
                : "Probe.CaptureItems(items)";

        return source.Replace(wildcardBody, replacement);
    }

    private static bool AreStructurallyEquivalent(string actual, string expected)
    {
        var actualTree = CSharpSyntaxTree.ParseText(actual);
        var expectedTree = CSharpSyntaxTree.ParseText(expected);
        return actualTree.GetRoot().NormalizeWhitespace().IsEquivalentTo(
            expectedTree.GetRoot().NormalizeWhitespace());
    }

    private sealed class WorkspaceState : IDisposable
    {
        public WorkspaceState(
            AdhocWorkspace workspace,
            ProjectId projectId,
            DocumentId userDocumentId)
        {
            Workspace = workspace;
            ProjectId = projectId;
            UserDocumentId = userDocumentId;
        }

        public AdhocWorkspace Workspace { get; }
        public ProjectId ProjectId { get; }
        public DocumentId UserDocumentId { get; }

        public void Dispose() => Workspace.Dispose();
    }
}
