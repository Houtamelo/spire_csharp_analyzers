using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
#if SPIRE_UNITY_TEST_HOST
using Spire.Tests.Hosting;
#endif

namespace Houtamelo.Spire.Analyzers.Tests;

/// Executes the hand-written wide enum-to-enum codefix pairs through the
/// emitted fixed source instead of stopping at syntax equivalence.
public sealed class EnumCodeFixCorrectnessTests
{
    private static readonly ImmutableArray<Case> Cases = ImmutableArray.Create(
        new Case("NonFlags_EnumToEnum_SByteSource", "SourceSByte", "TargetSByte", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_ByteSource", "SourceByte", "TargetByte", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_ShortSource", "SourceShort", "TargetShort", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_UShortSource", "SourceUShort", "TargetUShort", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_IntSource", "SourceInt", "TargetInt", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_UIntSource", "SourceUInt", "TargetUInt", "Value", "Value", true, false),
        new Case("NonFlags_EnumToEnum_LongSource", "SourceLong", "TargetLong", null, "Value", false, true),
        new Case("NonFlags_EnumToEnum_ULongSource", "SourceULong", "TargetULong", "Value", "Value", true, false),
        new Case("Flags_EnumToEnum_LongSource", "SourceLongFlags", "TargetLongFlags", "Combined", "Combined", true, false),
        new Case("Flags_EnumToEnum_ULongSource", "SourceULongFlags", "TargetULongFlags", "Combined", "Combined", true, false),
        new Case("NonFlags_EnumToEnum_LongConditionalSource", "SourceLongConditional", "TargetLongConditional", "true", "Value", false, true));

    [Fact]
    public async Task WideEnumCodeFixPairsCompileAndExecute()
    {
        foreach (var testCase in Cases)
            await VerifyCaseAsync(testCase);
    }

    private static async Task VerifyCaseAsync(Case testCase)
    {
        var casesDirectory = GetCasesDirectory();
        var beforeSource = File.ReadAllText(Path.Combine(casesDirectory, testCase.Name, "before.cs"));
        var afterSource = File.ReadAllText(Path.Combine(casesDirectory, testCase.Name, "after.cs"));

        var references = await ResolveReferencesAsync();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "EnumCodeFix", "EnumCodeFix", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, parseOptions)
            .WithProjectCompilationOptions(projectId, compilationOptions);
        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);

        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "case.cs", beforeSource);

        var document = solution.GetDocument(documentId)!;
        var compilation = (await document.Project.GetCompilationAsync())!;
        Assert.Empty(
            compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(value => value.Id == "SPIRE016");
        var provider = new FromEnumCodeFix();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));
        Assert.Single(actions);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();

        var fixedText = (await applyOperation.ChangedSolution
            .GetDocument(documentId)!
            .GetTextAsync())
            .ToString();
        Assert.True(
            CSharpSyntaxTree.ParseText(fixedText).GetRoot().NormalizeWhitespace().IsEquivalentTo(
                CSharpSyntaxTree.ParseText(afterSource).GetRoot().NormalizeWhitespace()),
            $"Unexpected fixed source for {testCase.Name}:\n{fixedText}");

        var fixedTree = CSharpSyntaxTree.ParseText(fixedText, parseOptions, path: "case.cs");
        var fixedCompilation = CSharpCompilation.Create(
            $"EnumCodeFixAfter_{testCase.Name}",
            new[] { fixedTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = Emit(fixedCompilation);
        var assembly = Assembly.Load(emit);
        var consumer = assembly.GetType("Consumer")!;
        var sourceType = assembly.GetType(testCase.SourceType)!;
        var targetType = assembly.GetType(testCase.TargetType)!;
        var method = consumer.GetMethod("Convert")!;

        object?[] arguments;
        if (testCase.SourceMember == "true")
        {
            arguments = new object?[] { true };
        }
        else if (testCase.SourceMember is null)
        {
            arguments = Array.Empty<object?>();
        }
        else
        {
            arguments = new[] { Enum.Parse(sourceType, testCase.SourceMember) };
        }

        var result = method.Invoke(null, arguments);
        Assert.NotNull(result);
        Assert.Equal(testCase.TargetMember, Enum.GetName(targetType, result!));

        if (testCase.HasEnumParameter)
        {
            var invalidSource = Enum.ToObject(sourceType, 8);
            var invocation = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object?[] { invalidSource }));
            Assert.IsType<ArgumentOutOfRangeException>(invocation.InnerException);
        }

        if (testCase.ExpectsSingleEvaluation)
        {
            var calls = (int)consumer.GetProperty("Calls")!.GetValue(null)!;
            Assert.Equal(1, calls);
        }
    }

    private static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
#if SPIRE_UNITY_TEST_HOST
        return UnityTestHost.RuntimeReferences
            .Add(MetadataReference.CreateFromFile(typeof(FromEnumCodeFix).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(SPIRE016InvalidEnforceInitializationEnumValueAnalyzer).Assembly.Location));
#else
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);
        return references
            .Add(MetadataReference.CreateFromFile(typeof(FromEnumCodeFix).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(SPIRE016InvalidEnforceInitializationEnumValueAnalyzer).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location));
#endif
    }

    private static string GetCasesDirectory()
    {
#if SPIRE_UNITY_TEST_HOST
        return Path.Combine(
            UnityTestHost.DataRoot(typeof(EnumCodeFixCorrectnessTests).Assembly),
            "SPIRE016CodeFix",
            "cases");
#else
        return Path.Combine(AppContext.BaseDirectory, "SPIRE016CodeFix", "cases");
#endif
    }

    private sealed record Case(
        string Name,
        string SourceType,
        string TargetType,
        string? SourceMember,
        string TargetMember,
        bool HasEnumParameter,
        bool ExpectsSingleEvaluation);
}
