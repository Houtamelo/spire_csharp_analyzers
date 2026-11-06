using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

/// Integration regressions for statement fixes at the closing brace.
public sealed class PendingSwitchFormattingTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private OptionSet _formattingOptions;

    public PendingSwitchFormattingTests()
    {
        _formattingOptions = _workspace.Options;
    }

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r\n", false)]
    [InlineData("\r\n", true)]
    public async Task GeneratedUnion_EmptyStatementHasNoBlankLineBeforeClose(
        string lineEnding,
        bool useTabs)
    {
        ConfigureFormatting(lineEnding, useTabs);
        var fixedSource = await ApplyFixAsync(
            GeneratedUnionSource(lineEnding, useTabs),
            generated: true);

        AssertNoWhitespaceOnlyBlankLineBeforeClose(fixedSource);
        Assert.Contains("System.NotImplementedException", fixedSource, StringComparison.Ordinal);
        Assert.Contains(useTabs ? "\t\tcase" : "        case", fixedSource, StringComparison.Ordinal);
        AssertLineEndings(fixedSource, lineEnding);
    }

    [Fact]
    public async Task EnforceHierarchy_EmptyStatementHasNoBlankLineBeforeClose()
    {
        ConfigureFormatting("\n", useTabs: false);
        var fixedSource = await ApplyFixAsync(
            EnforceHierarchySource("\n", "    "),
            generated: false);

        AssertNoWhitespaceOnlyBlankLineBeforeClose(fixedSource);
        Assert.Contains("case IdleState:", fixedSource, StringComparison.Ordinal);
        Assert.Contains("case MovingState:", fixedSource, StringComparison.Ordinal);
        Assert.Contains("case SpeakingState:", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCase_AppendsMissingCaseWithoutBlankLine()
    {
        ConfigureFormatting("\n", useTabs: false);
        var fixedSource = await ApplyFixAsync(
            EnforceHierarchySourceWithExistingCase(),
            generated: false);

        AssertNoWhitespaceOnlyBlankLineBeforeClose(fixedSource);
        Assert.Contains("case IdleState:", fixedSource, StringComparison.Ordinal);
        Assert.Contains("// existing case", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCommentBeforeClose_IsPreserved()
    {
        ConfigureFormatting("\n", useTabs: false);
        var fixedSource = await ApplyFixAsync(
            EnforceHierarchySourceWithComment(),
            generated: false);

        Assert.Contains("// keep this note", fixedSource, StringComparison.Ordinal);
        Assert.Contains("case IdleState:", fixedSource, StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task ExistingDirectiveBeforeClose_IsPreserved()
    {
        ConfigureFormatting("\n", useTabs: false);
        var fixedSource = await ApplyFixAsync(
            EnforceHierarchySourceWithDirective(),
            generated: false);

        Assert.Contains("#if true", fixedSource, StringComparison.Ordinal);
        Assert.Contains("#endif", fixedSource, StringComparison.Ordinal);
        Assert.Contains("System.NotImplementedException", fixedSource, StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    private async Task<string> ApplyFixAsync(string source, bool generated)
    {
        Compilation compilation;
        if (generated)
        {
            GeneratorTestHelper.RunGenerator(
                source,
                out compilation,
                out var generatorDiagnostics,
                path: "case.cs");
            GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        }
        else
        {
            compilation = CreateCompilation(source);
        }

        AssertNoErrors(compilation);
        var (document, _) = CreateDocumentFromCompilation(compilation);
        var diagnostic = await GetSingleDiagnosticAsync(compilation, document);
        var fixedSource = await ApplyFirstFixAsync(document, diagnostic);

        Compilation fixedCompilation;
        if (generated)
        {
            GeneratorTestHelper.RunGenerator(
                fixedSource,
                out fixedCompilation,
                out var generatorDiagnostics,
                path: "case.cs");
            GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        }
        else
        {
            fixedCompilation = CreateCompilation(fixedSource);
        }

        AssertNoErrors(fixedCompilation);
        using var stream = new MemoryStream();
        var emit = fixedCompilation.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics.Select(d =>
                $"{d.Id}: {d.GetMessage()}")));

        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.Empty(remaining);
        return fixedSource;
    }

    private static async Task<Microsoft.CodeAnalysis.Diagnostic> GetSingleDiagnosticAsync(
        Compilation compilation,
        Document document)
    {
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        var filePath = (await document.GetSyntaxTreeAsync())!.FilePath;
        return Assert.Single(diagnostics.Where(d =>
            d.Id == "SPIRE009" &&
            d.Location.SourceTree?.FilePath == filePath));
    }

    private async Task<string> ApplyFirstFixAsync(
        Document document,
        Microsoft.CodeAnalysis.Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await new AddMissingArmsCodeFix().RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changed = apply.ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    private (Document Document, Compilation Compilation) CreateDocumentFromCompilation(
        Compilation compilation)
    {
        var projectId = ProjectId.CreateNewId();
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var solution = _workspace.CurrentSolution
            .WithOptions(_formattingOptions)
            .AddProject(
                projectId,
                "PendingSwitchFormatting",
                "PendingSwitchFormatting",
                LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, parseOptions)
            .WithProjectCompilationOptions(
                projectId,
                (CSharpCompilationOptions)compilation.Options);
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? documentId = null;
        var treeIndex = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var id = DocumentId.CreateNewId(projectId);
            var filePath = string.IsNullOrEmpty(tree.FilePath)
                ? $"generated-{treeIndex}.g.cs"
                : tree.FilePath;
            solution = solution.AddDocument(
                id,
                Path.GetFileName(filePath),
                tree.GetText(),
                filePath: filePath);
            if (tree.FilePath == "case.cs" || documentId is null)
                documentId = id;
            treeIndex++;
        }

        Assert.NotNull(documentId);
        return (solution.GetDocument(documentId!)!, compilation);
    }

    private void ConfigureFormatting(string lineEnding, bool useTabs)
    {
        _formattingOptions = _formattingOptions
            .WithChangedOption(
                FormattingOptions.NewLine,
                LanguageNames.CSharp,
                lineEnding)
            .WithChangedOption(
                FormattingOptions.UseTabs,
                LanguageNames.CSharp,
                useTabs)
            .WithChangedOption(
                FormattingOptions.TabSize,
                LanguageNames.CSharp,
                4)
            .WithChangedOption(
                FormattingOptions.IndentationSize,
                LanguageNames.CSharp,
                4);
    }

    private static Compilation CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "case.cs");
        var references = TestHarnessEnvironment.FrameworkReferences
            .Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location));
        return CSharpCompilation.Create(
            "PendingSwitchFormatting",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static void AssertNoWhitespaceOnlyBlankLineBeforeClose(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var switchStatement = Assert.Single(
            root.DescendantNodes().OfType<SwitchStatementSyntax>());
        var closeBrace = switchStatement.CloseBraceToken;
        var previousToken = closeBrace.GetPreviousToken();
        var between = previousToken.TrailingTrivia.ToFullString() +
            closeBrace.LeadingTrivia.ToFullString();
        var normalized = between.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(1, normalized.Count(value => value == '\n'));
    }

    private static void AssertLineEndings(string source, string lineEnding)
    {
        if (lineEnding == "\r\n")
        {
            Assert.DoesNotContain(
                "\n",
                source.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        }
        else
        {
            Assert.DoesNotContain("\r", source, StringComparison.Ordinal);
        }
    }

    private static void AssertNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static string GeneratedUnionSource(string lineEnding, bool useTabs)
    {
        var indent = useTabs ? "\t" : "    ";
        var body = indent + indent;
        var blankLine = body + indent;
        return string.Join(lineEnding, new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[DiscriminatedUnion]",
            "public partial struct State",
            "{",
            $"{indent}[Variant] public static partial State Idle();",
            $"{indent}[Variant] public static partial State Moving(int destination);",
            $"{indent}[Variant] public static partial State Speaking(string text);",
            "}",
            "",
            "public static class Consumer",
            "{",
            $"{indent}public static void Read(State state)",
            $"{indent}{{",
            $"{body}switch (state)",
            $"{body}{{",
            blankLine,
            $"{body}}}",
            $"{indent}}}",
            "}",
        });
    }

    private static string EnforceHierarchySource(string lineEnding, string indent)
    {
        var body = indent + indent;
        var blankLine = body + indent;
        return string.Join(lineEnding, new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[EnforceExhaustiveness]",
            "public abstract record BaseState;",
            "public sealed record IdleState : BaseState;",
            "public sealed record MovingState : BaseState;",
            "public sealed record SpeakingState : BaseState;",
            "",
            "public static class Consumer",
            "{",
            $"{indent}public static void Read(BaseState state)",
            $"{indent}{{",
            $"{body}switch (state)",
            $"{body}{{",
            blankLine,
            $"{body}}}",
            $"{indent}}}",
            "}",
        });
    }

    private static string EnforceHierarchySourceWithExistingCase()
    {
        return string.Join("\n", new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[EnforceExhaustiveness]",
            "public abstract record BaseState;",
            "public sealed record IdleState : BaseState;",
            "public sealed record MovingState : BaseState;",
            "",
            "public static class Consumer",
            "{",
            "    public static void Read(BaseState state)",
            "    {",
            "        switch (state)",
            "        {",
            "            case IdleState:",
            "                break; // existing case",
            "            ",
            "        }",
            "    }",
            "}",
        });
    }

    private static string EnforceHierarchySourceWithComment()
    {
        return string.Join("\n", new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[EnforceExhaustiveness]",
            "public abstract record BaseState;",
            "public sealed record IdleState : BaseState;",
            "public sealed record MovingState : BaseState;",
            "",
            "public static class Consumer",
            "{",
            "    public static void Read(BaseState state)",
            "    {",
            "        switch (state)",
            "        {",
            "            // keep this note",
            "            ",
            "        }",
            "    }",
            "}",
        });
    }

    private static string EnforceHierarchySourceWithDirective()
    {
        return string.Join("\n", new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[EnforceExhaustiveness]",
            "public abstract record BaseState;",
            "public sealed record IdleState : BaseState;",
            "public sealed record MovingState : BaseState;",
            "",
            "public static class Consumer",
            "{",
            "    public static void Read(BaseState state)",
            "    {",
            "        switch (state)",
            "        {",
            "\u0023if true",
            "            // keep this directive",
            "\u0023endif",
            "            ",
            "        }",
            "    }",
            "}",
        });
    }
}
