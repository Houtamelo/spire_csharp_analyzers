using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReviewRegressions;

public sealed class ExpandWildcardRefactoringTests
{
    [Fact]
    public async Task StructUnion_ExplicitArmAndWildcard_ExpandsAndEmits()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha(int value);
                [Variant] public static partial Choice Beta(string text);
            }

            public static class Reader
            {
                public static string Describe(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Alpha, alpha: { value: var alphaValue } } => $"alpha:{alphaValue}",
                    _ => "other",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: true);
        Assert.Equal(1, result.ActionCount);
        Assert.NotNull(result.ChangedSource);
        var changedSource = result.ChangedSource!;

        Assert.Contains("{ kind: Choice.Kind.Beta", changedSource, StringComparison.Ordinal);
        Assert.Contains("beta: { text: var text }", changedSource, StringComparison.Ordinal);
        AssertCompilesAndEmits(changedSource, "struct-wildcard.cs");
    }

    [Fact]
    public async Task RecordUnion_ExplicitArmAndWildcard_ExpandsAndEmits()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial record Result
            {
                public sealed partial record Count(int Value) : Result;
                public sealed partial record Text(string Message) : Result;
            }

            public static class Reader
            {
                public static string Describe(Result result) => result switch
                {
                    Result.Count(var count) => $"count:{count}",
                    _ => "other",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: true);
        Assert.Equal(1, result.ActionCount);
        Assert.NotNull(result.ChangedSource);
        var changedSource = result.ChangedSource!;

        Assert.Contains("Result.Text", changedSource, StringComparison.Ordinal);
        Assert.Contains("=> \"other\"", changedSource, StringComparison.Ordinal);
        AssertCompilesAndEmits(changedSource, "record-wildcard.cs");
    }

    [Fact]
    public async Task WildcardBody_IsPreservedForEveryMissingStructVariant()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha();
                [Variant] public static partial Choice Beta();
                [Variant] public static partial Choice Gamma();
            }

            public static class Reader
            {
                public static string Describe(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Alpha } => "alpha",
                    _ => "fallback-body",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: true);
        Assert.Equal(1, result.ActionCount);
        Assert.NotNull(result.ChangedSource);
        var changedSource = result.ChangedSource!;

        Assert.Equal(2, CountOccurrences(changedSource, "=> \"fallback-body\""));
        AssertCompilesAndEmits(changedSource, "wildcard-body.cs");
    }

    [Fact]
    public async Task StructUnion_CaptureCollision_UsesUniqueCaptureName()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Empty();
                [Variant] public static partial Choice Some(int value);
            }

            public static class Reader
            {
                public static int Describe(Choice choice)
                {
                    var value = 41;
                    var value1 = 42;
                    return choice switch
                    {
                        { kind: Choice.Kind.Empty } => value + value1,
                        _ => value + value1,
                    };
                }
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: true);
        Assert.Equal(1, result.ActionCount);
        Assert.NotNull(result.ChangedSource);
        var changedSource = result.ChangedSource!;

        Assert.Contains("some: { value: var value2 }", changedSource, StringComparison.Ordinal);
        AssertCompilesAndEmits(changedSource, "capture-collision.cs");
    }

    [Fact]
    public async Task GenericStructUnion_WildcardExpansion_Emits()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Option<T>
            {
                [Variant] public static partial Option<T> Some(T value);
                [Variant] public static partial Option<T> None();
            }

            public static class Reader
            {
                public static string Describe<T>(Option<T> option) => option switch
                {
                    { kind: Option<T>.Kind.None } => "none",
                    _ => "some",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: true);
        Assert.Equal(1, result.ActionCount);
        Assert.NotNull(result.ChangedSource);
        var changedSource = result.ChangedSource!;

        Assert.Contains("Option<T>.Kind.Some", changedSource, StringComparison.Ordinal);
        AssertCompilesAndEmits(changedSource, "generic-wildcard.cs");
    }

    [Fact]
    public async Task CursorOnExplicitArm_DoesNotOfferRefactoring()
    {
        var result = await RunRefactoringAsync(
            StructSource,
            root => root.DescendantNodes()
                .OfType<SwitchExpressionArmSyntax>()
                .Single(arm => arm.Pattern is RecursivePatternSyntax)
                .Pattern
                .Span,
            apply: false);

        Assert.Equal(0, result.ActionCount);
        Assert.Null(result.ChangedSource);
    }

    [Fact]
    public async Task CursorOutsideWildcard_DoesNotOfferRefactoring()
    {
        var result = await RunRefactoringAsync(
            StructSource,
            root => root.DescendantNodes()
                .OfType<SwitchExpressionSyntax>()
                .Single()
                .GoverningExpression
                .Span,
            apply: false);

        Assert.Equal(0, result.ActionCount);
        Assert.Null(result.ChangedSource);
    }

    [Fact]
    public async Task NonDiscriminatedUnionWildcard_DoesNotOfferRefactoring()
    {
        var source = """
            public static class Reader
            {
                public static string Describe(int value) => value switch
                {
                    0 => "zero",
                    _ => "other",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: false);

        Assert.Equal(0, result.ActionCount);
        Assert.Null(result.ChangedSource);
    }

    [Fact]
    public async Task SwitchStatementWildcard_DoesNotOfferRefactoring()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha();
                [Variant] public static partial Choice Beta();
            }

            public static class Reader
            {
                public static int Describe(Choice choice)
                {
                    switch (choice)
                    {
                        case { kind: Choice.Kind.Alpha }:
                            return 1;
                        case var _:
                            return 0;
                    }
                }
            }
            """;

        var result = await RunRefactoringAsync(
            source,
            root => root.DescendantNodes()
                .OfType<CasePatternSwitchLabelSyntax>()
                .Single(label => label.Pattern is VarPatternSyntax)
                .Pattern
                .Span,
            apply: false);

        Assert.Equal(0, result.ActionCount);
        Assert.Null(result.ChangedSource);
    }

    [Fact]
    public async Task AlreadyFullyCoveredWildcard_DoesNotOfferRefactoring()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Choice
            {
                [Variant] public static partial Choice Alpha();
                [Variant] public static partial Choice Beta();
            }

            public static class Reader
            {
                public static string Describe(Choice choice) => choice switch
                {
                    { kind: Choice.Kind.Alpha } => "alpha",
                    { kind: Choice.Kind.Beta } => "beta",
                    _ => "fallback",
                };
            }
            """;

        var result = await RunRefactoringAsync(source, WildcardSpan, apply: false);

        Assert.Equal(0, result.ActionCount);
        Assert.Null(result.ChangedSource);
    }

    private static string StructSource => """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Choice
        {
            [Variant] public static partial Choice Alpha(int value);
            [Variant] public static partial Choice Beta(string text);
        }

        public static class Reader
        {
            public static string Describe(Choice choice) => choice switch
            {
                { kind: Choice.Kind.Alpha, alpha: { value: var alphaValue } } => $"alpha:{alphaValue}",
                _ => "other",
            };
        }
        """;

    private static async Task<(int ActionCount, string? ChangedSource)> RunRefactoringAsync(
        string source,
        Func<SyntaxNode, TextSpan> cursorSelector,
        bool apply)
    {
        const string userPath = "expand-wildcard.cs";

        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: userPath);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ExpandWildcardReview", "ExpandWildcardReview", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(
                projectId,
                (CSharpParseOptions)outputCompilation.SyntaxTrees.First().Options);

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
            if (string.Equals(tree.FilePath, userPath, StringComparison.Ordinal) ||
                string.Equals(Path.GetFileName(tree.FilePath), userPath, StringComparison.Ordinal))
            {
                userDocumentId = documentId;
            }
        }

        Assert.NotNull(userDocumentId);

        var document = solution.GetDocument(userDocumentId!)!;
        var root = await document.GetSyntaxRootAsync();
        Assert.NotNull(root);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            cursorSelector(root!),
            action => actions.Add(action),
            CancellationToken.None);
        await new ExpandWildcardRefactoring().ComputeRefactoringsAsync(context);

        if (!apply)
            return (actions.Count, null);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOperation = Assert.Single(operations.OfType<ApplyChangesOperation>());

        // Inspect the refactoring's immutable result without depending on workspace application.
        var changedDocument = applyOperation.ChangedSolution.GetDocument(userDocumentId!)!;
        var changedSource = (await changedDocument.GetTextAsync()).ToString();
        return (actions.Count, changedSource);
    }

    private static TextSpan WildcardSpan(SyntaxNode root)
        => root.DescendantNodes()
            .OfType<DiscardPatternSyntax>()
            .Single(pattern => pattern.Parent is SwitchExpressionArmSyntax)
            .Span;

    private static void AssertCompilesAndEmits(string source, string path)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: path);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }

        return count;
    }
}
