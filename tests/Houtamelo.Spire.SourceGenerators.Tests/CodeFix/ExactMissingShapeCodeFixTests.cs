using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

/// Real workspace checks for the code fix's diagnostic contract. Snapshot cases
/// exercise formatting; these checks also recompile and reanalyze the result.
public sealed class ExactMissingShapeCodeFixTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    public static IEnumerable<object[]> ExactSnapshotCases()
    {
        var root = Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "CodeFix",
            "cases");
        if (!Directory.Exists(root))
            yield break;

        foreach (var directory in Directory.GetDirectories(root)
                     .Where(path =>
                         Path.GetFileName(path).StartsWith(
                             "SPIRE015AddMissingEnumMembers_",
                             StringComparison.Ordinal) ||
                         Path.GetFileName(path).StartsWith(
                             "SPIRE009_Exact_",
                             StringComparison.Ordinal)))
        {
            if (File.Exists(Path.Combine(directory, "before.cs")) &&
                File.Exists(Path.Combine(directory, "after.cs")))
            {
                yield return new object[] { Path.GetFileName(directory) };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExactSnapshotCases))]
    public async Task ExactSnapshots_CompileEmitAndClearDiagnostic(string caseName)
    {
        var directory = Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "CodeFix",
            "cases",
            caseName);
        var before = File.ReadAllText(Path.Combine(directory, "before.cs"));
        var expected = File.ReadAllText(Path.Combine(directory, "after.cs"));
        Document document;
        Compilation compilation;
        DiagnosticAnalyzer analyzer;
        if (caseName.StartsWith("SPIRE015", StringComparison.Ordinal))
        {
            (document, compilation) = CreateDocument(before);
            analyzer = new SPIRE015ExhaustiveEnumSwitchAnalyzer();
        }
        else
        {
            GeneratorTestHelper.RunGenerator(
                before,
                out compilation,
                out var generatorDiagnostics,
                path: "case.cs");
            GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
            document = CreateDocumentFromCompilation(compilation).Document;
            analyzer = new ExhaustivenessAnalyzer();
        }

        AssertNoErrors(compilation);
        var diagnostic = await GetSingleDiagnosticAsync(compilation, document, analyzer);
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.True(
            GeneratorTestHelper.AreStructurallyEquivalent(fixedSource, expected),
            $"Unexpected fixed source for {caseName}:\n{fixedSource}");

        Compilation fixedCompilation;
        if (caseName.StartsWith("SPIRE015", StringComparison.Ordinal))
        {
            fixedCompilation = CreateCompilation(fixedSource);
        }
        else
        {
            GeneratorTestHelper.RunGenerator(
                fixedSource,
                out fixedCompilation,
                out var generatorDiagnostics,
                path: "case.cs");
            GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        }

        AssertNoErrors(fixedCompilation);
        using var stream = new MemoryStream();
        var emit = fixedCompilation.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics.Select(d =>
                $"{d.Id}: {d.GetMessage()}")));
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer))
            .GetAnalyzerDiagnosticsAsync();
        var expectedId = analyzer is SPIRE015ExhaustiveEnumSwitchAnalyzer
            ? "SPIRE015"
            : "SPIRE009";
        Assert.DoesNotContain(remaining, d => d.Id == expectedId);
    }

    [Fact]
    public async Task SPIRE015_SwitchStatement_FixedSourceCompilesAndClearsDiagnostic()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum State { Idle, Moving, Attacking }

            static class Consumer
            {
                public static void Update(State state)
                {
                    switch (state)
                    {
                        case State.Idle:
                            break;
                        case State.Moving:
                        {
                            break;
                        }
                    }
                }
            }
            """;

        var (document, _) = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());

        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("State.Attacking", fixedSource, StringComparison.Ordinal);
        Assert.Contains("System.NotImplementedException", fixedSource, StringComparison.Ordinal);

        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE015_MixedBodies_InsertsSiblingSectionWithStatementIndentation()
    {
        var source = File.ReadAllText(Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "CodeFix",
            "cases",
            "SPIRE015AddMissingEnumMembers_SwitchStatement_MixedBodies",
            "before.cs"));
        var (document, _) = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Matches(
            @"(?m)^[ ]{12}case State\.Attacking:\r?\n[ ]{16}throw new System\.NotImplementedException\(\);",
            fixedSource);
    }

    [Fact]
    public async Task SPIRE015_InlineEmptySwitchStatement_UsesContainingIndentation()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum State { Idle, Moving, Speaking }

            class Consumer
            {
                static void Tick(State state)
                {
                    switch (state) {}
                }
            }
            """;

        ConfigureFormatting("\n", useTabs: false);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        const string expectedSwitch = """
                    switch (state) {
                        case State.Idle:
                            throw new System.NotImplementedException();
                        case State.Moving:
                            throw new System.NotImplementedException();
                        case State.Speaking:
                            throw new System.NotImplementedException();
                    }
            """;
        Assert.Contains(expectedSwitch, fixedSource, StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
        var remaining = await CreateCompilation(fixedSource)
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE015_MultilineEmptySwitchStatement_UsesContainingIndentation()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum State { Idle, Moving, Speaking }

            class Consumer
            {
                static void Tick(State state)
                {
                    switch (state)
                    {
                    }
                }
            }
            """;

        ConfigureFormatting("\n", useTabs: false);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        const string expectedSwitch = """
                    switch (state)
                    {
                        case State.Idle:
                            throw new System.NotImplementedException();
                        case State.Moving:
                            throw new System.NotImplementedException();
                        case State.Speaking:
                            throw new System.NotImplementedException();
                    }
            """;
        Assert.Contains(expectedSwitch, fixedSource, StringComparison.Ordinal);
        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE009_InlineEmptySwitchStatement_UsesContainingIndentation()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct State
            {
                [Variant] public static partial State Idle();
                [Variant] public static partial State Moving(int destination, string marker);
                [Variant] public static partial State Speaking(string text, float timer);
            }

            class Consumer
            {
                static void Tick(State state)
                {
                    switch (state) {}
                }
            }
            """;

        ConfigureFormatting("\n", useTabs: false);
        GeneratorTestHelper.RunGenerator(
            source,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedCompilation);
        var (document, _) = CreateDocumentFromCompilation(generatedCompilation);
        var diagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            document,
            new ExhaustivenessAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        const string expectedSwitch = """
                    switch (state) {
                        case { IsIdle: true }:
                            throw new System.NotImplementedException();
                        case { IsMoving: true, moving: var (destination, marker) }:
                            throw new System.NotImplementedException();
                        case { IsSpeaking: true, speaking: var (text, timer) }:
                            throw new System.NotImplementedException();
                    }
            """;
        Assert.Contains(expectedSwitch, fixedSource, StringComparison.Ordinal);
        var fixedGeneratedCompilation = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedGeneratedCompilation);
        var remaining = await fixedGeneratedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE009");
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    public async Task SPIRE015_InlineEmptySwitchStatement_PreservesConfiguredLineEndingAndIndentation(
        string lineEnding,
        bool useTabs)
    {
        var unit = useTabs ? "\t" : "    ";
        var source = string.Join(lineEnding, new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[EnforceExhaustiveness]",
            "enum State { Idle, Moving }",
            "",
            "class Consumer",
            "{",
            $"{unit}static void Tick(State state)",
            $"{unit}{{",
            $"{unit}{unit}switch (state) {{}}",
            $"{unit}}}",
            "}",
            "",
        });

        ConfigureFormatting(lineEnding, useTabs);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        var expectedSwitch = string.Join(lineEnding, new[]
        {
            $"{unit}{unit}switch (state) {{",
            $"{unit}{unit}{unit}case State.Idle:",
            $"{unit}{unit}{unit}{unit}throw new System.NotImplementedException();",
            $"{unit}{unit}{unit}case State.Moving:",
            $"{unit}{unit}{unit}{unit}throw new System.NotImplementedException();",
            $"{unit}{unit}}}",
        });
        Assert.Contains(expectedSwitch, fixedSource, StringComparison.Ordinal);
        if (lineEnding == "\n")
            Assert.DoesNotContain("\r", fixedSource, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("\n", fixedSource.Replace("\r\n", String.Empty), StringComparison.Ordinal);

        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    public async Task SPIRE009_InlineEmptySwitchStatement_PreservesConfiguredLineEndingAndIndentation(
        string lineEnding,
        bool useTabs)
    {
        var unit = useTabs ? "\t" : "    ";
        var source = string.Join(lineEnding, new[]
        {
            "using Houtamelo.Spire;",
            "",
            "[DiscriminatedUnion]",
            "partial struct State",
            "{",
            $"{unit}[Variant] public static partial State Idle();",
            $"{unit}[Variant] public static partial State Moving(int destination, string marker);",
            $"{unit}[Variant] public static partial State Speaking(string text, int timer);",
            "}",
            "",
            "class Consumer",
            "{",
            $"{unit}static void Tick(State state)",
            $"{unit}{{",
            $"{unit}{unit}switch (state) {{}}",
            $"{unit}}}",
            "}",
            "",
        });

        ConfigureFormatting(lineEnding, useTabs);
        GeneratorTestHelper.RunGenerator(
            source,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedCompilation);
        var (document, _) = CreateDocumentFromCompilation(generatedCompilation);
        var diagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            document,
            new ExhaustivenessAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        var expectedSwitch = string.Join(lineEnding, new[]
        {
            $"{unit}{unit}switch (state) {{",
            $"{unit}{unit}{unit}case {{ IsIdle: true }}:",
            $"{unit}{unit}{unit}{unit}throw new System.NotImplementedException();",
            $"{unit}{unit}{unit}case {{ IsMoving: true, moving: var (destination, marker) }}:",
            $"{unit}{unit}{unit}{unit}throw new System.NotImplementedException();",
            $"{unit}{unit}{unit}case {{ IsSpeaking: true, speaking: var (text, timer) }}:",
            $"{unit}{unit}{unit}{unit}throw new System.NotImplementedException();",
            $"{unit}{unit}}}",
        });
        Assert.Contains(expectedSwitch, fixedSource, StringComparison.Ordinal);
        if (lineEnding == "\n")
            Assert.DoesNotContain("\r", fixedSource, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("\n", fixedSource.Replace("\r\n", String.Empty), StringComparison.Ordinal);

        var fixedGeneratedCompilation = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedGeneratedCompilation);
        var remaining = await fixedGeneratedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE009");
    }

    [Fact]
    public async Task SPIRE015_InlineEmptySwitchStatement_PreservesBlockComment()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum State { Idle, Moving }

            class Consumer
            {
                static void Tick(State state)
                {
                    switch (state) { /* keep this block comment */ }
                }
            }
            """;

        ConfigureFormatting("\n", useTabs: false);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("/* keep this block comment */", fixedSource, StringComparison.Ordinal);
        Assert.True(
            fixedSource.IndexOf("/* keep this block comment */", StringComparison.Ordinal) <
            fixedSource.IndexOf("case State.Idle:", StringComparison.Ordinal));
        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE015_InlineEmptySwitchStatement_PreservesTrailingComment()
    {
        const string source = "using Houtamelo.Spire;\n\n" +
            "[EnforceExhaustiveness]\n" +
            "enum State { Idle, Moving }\n\n" +
            "class Consumer\n" +
            "{\n" +
            "    static void Tick(State state)\n" +
            "    {\n" +
            "        switch (state) { // keep this trailing comment\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        ConfigureFormatting("\n", useTabs: false);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("// keep this trailing comment", fixedSource, StringComparison.Ordinal);
        Assert.True(
            fixedSource.IndexOf("// keep this trailing comment", StringComparison.Ordinal) <
            fixedSource.IndexOf("case State.Idle:", StringComparison.Ordinal));
        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE015_InlineEmptySwitchStatement_PreservesClosingBraceTrailingComment()
    {
        const string source = "using Houtamelo.Spire;\n\n" +
            "[EnforceExhaustiveness]\n" +
            "enum State { Idle, Moving }\n\n" +
            "class Consumer\n" +
            "{\n" +
            "    static void Tick(State state)\n" +
            "    {\n" +
            "        switch (state) {} // keep this closing brace comment\n" +
            "    }\n" +
            "}\n";

        ConfigureFormatting("\n", useTabs: false);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains(
            "        } // keep this closing brace comment",
            fixedSource,
            StringComparison.Ordinal);
        var fixedCompilation = CreateCompilation(fixedSource);
        AssertNoErrors(fixedCompilation);
        var remaining = await fixedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task SPIRE015_NullableEnum_SeparatesEscapedMemberFromNull()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum Token { @null, Done }

            static class Consumer
            {
                public static int Read(Token? token) => token switch
                {
                    Token.Done => 1,
                };
            }
            """;

        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());

        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("Token.@null", fixedSource, StringComparison.Ordinal);
        Assert.Contains("null =>", fixedSource, StringComparison.Ordinal);
        Assert.Equal(1, CountLiteralNullPatterns(fixedSource));
        Assert.Equal(1, CountTokenNullMemberPatterns(fixedSource));
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task SPIRE015_NullableEnum_OnlyMemberMissing_DoesNotAddNull()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum Token { @null, Done }

            static class Consumer
            {
                public static int Read(Token? token) => token switch
                {
                    Token.Done => 1,
                    null => 0,
                };
            }
            """;

        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("Token.@null", fixedSource, StringComparison.Ordinal);
        Assert.Equal(1, CountLiteralNullPatterns(fixedSource));
        Assert.Equal(1, CountTokenNullMemberPatterns(fixedSource));
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task SPIRE015_NullableEnum_OnlyNullMissing_DoesNotAddEscapedMemberTwice()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum Token { @null, Done }

            static class Consumer
            {
                public static int Read(Token? token) => token switch
                {
                    Token.@null => 0,
                    Token.Done => 1,
                };
            }
            """;

        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("null =>", fixedSource, StringComparison.Ordinal);
        Assert.Equal(1, CountLiteralNullPatterns(fixedSource));
        Assert.Equal(1, CountTokenNullMemberPatterns(fixedSource));
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task SPIRE009_GeneratedUnion_FixedSourceCompilesAndClearsDiagnostic()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Token
            {
                [Variant] public static partial Token Ident(string name);
                [Variant] public static partial Token Eof();
            }

            static class Consumer
            {
                public static int Read(Token token) => token switch
                {
                    { IsIdent: true } => 1,
                };
            }
            """;

        GeneratorTestHelper.RunGenerator(
            source,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedCompilation);

        var (document, _) = CreateDocumentFromCompilation(generatedCompilation);
        var diagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            document,
            new ExhaustivenessAnalyzer());

        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("IsEof", fixedSource, StringComparison.Ordinal);
        var fixedGenerated = RunGeneratedCompilation(fixedSource);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedGenerated);
        var remaining = await fixedGenerated.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(remaining, d => d.Id == "SPIRE009");
    }

    [Fact]
    public async Task SPIRE009_ExactPatternMetadata_PreservesPayloadCorrelation()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
            {
                [Variant] public static partial Command A(bool flag);
                [Variant] public static partial Command B();
            }

            static class Consumer
            {
                public static int Read(Command command) => command switch
                {
                    { kind: Command.Kind.A, a: { flag: true } } => 1,
                    { IsB: true } => 2,
                };
            }
            """;

        GeneratorTestHelper.RunGenerator(
            source,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedCompilation);
        var (document, _) = CreateDocumentFromCompilation(generatedCompilation);
        var diagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            document,
            new ExhaustivenessAnalyzer());

        Assert.Equal("1", diagnostic.Properties["MissingPatternCount"]);
        var pattern = diagnostic.Properties["MissingPattern0"];
        Assert.NotNull(pattern);
        Assert.Contains("IsA", pattern!, StringComparison.Ordinal);
        Assert.Contains("flag", pattern!, StringComparison.Ordinal);
        Assert.Contains("false", pattern!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SPIRE009_DecimalGap_HasNoUnsoundBroadVariantAction()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
            {
                [Variant] public static partial Command A(decimal amount);
                [Variant] public static partial Command B();
            }

            static class Consumer
            {
                public static int Read(Command command) => command switch
                {
                    { kind: Command.Kind.A, a: { amount: < 0.1234567890123456789012345678m } } => 1,
                    { kind: Command.Kind.A, a: { amount: > 0.2234567890123456789012345678m } } => 2,
                    { kind: Command.Kind.B } => 3,
                };
            }
            """;

        GeneratorTestHelper.RunGenerator(
            source,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedCompilation);
        var (document, _) = CreateDocumentFromCompilation(generatedCompilation);
        var diagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            document,
            new ExhaustivenessAnalyzer());

        Assert.Equal("A", diagnostic.Properties["MissingVariants"]);
        Assert.False(diagnostic.Properties.ContainsKey("MissingPatternCount"));
        Assert.Empty(await GetActionsAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix()));
    }

    [Fact]
    public async Task SPIRE015_CrlfFix_PreservesRawStringTokenValue()
    {
        const string rawLiteral = "\"\"\"\nline one\nline two\n\"\"\"";
        var source = "using Houtamelo.Spire;\r\n\r\n" +
            "[EnforceExhaustiveness]\r\n" +
            "enum Color { Red, Green }\r\n\r\n" +
            "class Consumer\r\n{\r\n" +
            "public static string Banner = " + rawLiteral + ";\r\n" +
            "public static int Read(Color color) => color switch\r\n" +
            "{\r\n" +
            "    Color.Red => 1,\r\n" +
            "};\r\n" +
            "}\r\n";
        var beforeTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var beforeValue = GetRawLiteralValue(beforeTree);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        var afterTree = CSharpSyntaxTree.ParseText(
            fixedSource,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        Assert.Equal(beforeValue, GetRawLiteralValue(afterTree));
        Assert.Contains("Color.Green", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SPIRE015_GlobalEnforcement_ControlsDiagnosticAndCodeFix()
    {
        const string source = """
            enum Color { Red, Green }

            static class Consumer
            {
                public static int Read(Color color) => color switch
                {
                    Color.Red => 1,
                };
            }
            """;

        var compilation = CreateCompilation(source);
        var (document, _) = CreateDocumentFromCompilation(compilation);
        var disabled = await AnalyzeWithGlobalOptionAsync(
            compilation,
            enabled: false);
        Assert.DoesNotContain(disabled, d => d.Id == "SPIRE015");

        var enabled = await AnalyzeWithGlobalOptionAsync(
            compilation,
            enabled: true);
        var diagnostic = Assert.Single(enabled.Where(d => d.Id == "SPIRE015"));
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());
        Assert.Contains("Color.Green", fixedSource, StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task SPIRE015_StatementTrivia_PreservesCommentsAndCrLf()
    {
        var source = "using Houtamelo.Spire;\r\n\r\n" +
            "[EnforceExhaustiveness]\r\n" +
            "enum State { Idle, Moving, Attacking }\r\n\r\n" +
            "class Consumer\r\n{\r\n" +
            "\tvoid Tick(State state)\r\n\t{\r\n" +
            "\t\tswitch (state)\r\n\t\t{\r\n" +
            "\t\t\t// Existing section\r\n" +
            "\t\t\tcase State.Idle:\r\n" +
            "\t\t\t\tbreak;\r\n" +
            "\t\t}\r\n\t}\r\n}\r\n";
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("// Existing section", fixedSource, StringComparison.Ordinal);
        Assert.Contains("State.Attacking", fixedSource, StringComparison.Ordinal);
        Assert.Contains("\r\n", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", fixedSource.Replace("\r\n", String.Empty), StringComparison.Ordinal);
        var attackingIndex = fixedSource.IndexOf("State.Attacking", StringComparison.Ordinal);
        var lineStart = fixedSource.LastIndexOf("\r\n", attackingIndex, StringComparison.Ordinal);
        Assert.True(lineStart >= 0);
        Assert.Contains("\t", fixedSource.Substring(lineStart, attackingIndex - lineStart));
    }

    [Theory]
    [InlineData("\n", true)]
    [InlineData("\n", false)]
    [InlineData("\r\n", true)]
    [InlineData("\r\n", false)]
    public async Task SPIRE015_ExpressionTrivia_PreservesCommentAndLineEnding(
        string lineEnding,
        bool trailingComma)
    {
        var source = "using Houtamelo.Spire;\n\n" +
            "[EnforceExhaustiveness]\n" +
            "enum Color { Red, Green, Blue }\n\n" +
            "class Consumer\n{\n" +
            "    int Read(Color color) => color switch\n    {\n" +
            "        Color.Red => 1, // keep this comment\n" +
            "        Color.Green => 2" + (trailingComma ? "," : String.Empty) +
                " // final arm comment\n" +
            "    };\n}\n";
        source = source.Replace("\n", lineEnding, StringComparison.Ordinal);
        var (document, compilation) = CreateDocument(source);
        var diagnostic = await GetSingleDiagnosticAsync(
            compilation,
            document,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var fixedSource = await ApplyFirstFixAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());

        Assert.Contains("// keep this comment", fixedSource, StringComparison.Ordinal);
        Assert.Contains("// final arm comment", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Color.Blue", fixedSource, StringComparison.Ordinal);
        Assert.Contains(lineEnding, fixedSource, StringComparison.Ordinal);
        if (lineEnding == "\n")
            Assert.DoesNotContain("\r", fixedSource, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("\n", fixedSource.Replace("\r\n", String.Empty), StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task SPIRE015_UnmarkedEnumWithoutGlobalPolicy_HasNoAction()
    {
        const string source = """
            enum Color { Red, Green }

            static class Consumer
            {
                public static int Read(Color color) => color switch
                {
                    Color.Red => 1,
                };
            }
            """;

        var (document, _) = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE015");

        var provider = new AddMissingArmsCodeFix();
        Assert.NotNull(provider.GetFixAllProvider());
    }

    [Fact]
    public async Task SPIRE015_MissingDiagnosticMetadata_HasNoAction()
    {
        const string source = "enum Color { Red, Green }";
        var (document, _) = CreateDocument(source);
        var descriptor = new DiagnosticDescriptor(
            "SPIRE015",
            "Exhaustive switch",
            "Missing member",
            "Correctness",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        var root = await document.GetSyntaxRootAsync();
        var diagnostic = RoslynDiagnostic.Create(
            descriptor,
            root!.GetLocation(),
            ImmutableDictionary<string, string?>.Empty);
        var actions = await GetActionsAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());
        Assert.Empty(actions);
    }

    [Fact]
    public async Task MalformedStructuredMetadataOrLocation_HasNoAction()
    {
        const string source = """
            using Houtamelo.Spire;
            [EnforceExhaustiveness]
            enum Color { Red, Green }
            class Consumer
            {
                int Read(Color color) => color switch
                {
                    Color.Red => 1,
                };
            }
            """;
        var (document, _) = CreateDocument(source);
        var root = await document.GetSyntaxRootAsync();
        var switchExpression = root!.DescendantNodes()
            .OfType<SwitchExpressionSyntax>()
            .Single();
        var descriptor = new DiagnosticDescriptor(
            "SPIRE015",
            "Exhaustive switch",
            "Missing member",
            "Correctness",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        var malformed = new[]
        {
            ImmutableDictionary<string, string?>.Empty
                .Add("MissingMembers", "Green")
                .Add("MissingPatternCount", "2")
                .Add("MissingPattern0", "Color.Green"),
            ImmutableDictionary<string, string?>.Empty
                .Add("MissingMembers", "Green")
                .Add("MissingPatternCount", "0")
                .Add("MissingPattern0", "Color.Green"),
            ImmutableDictionary<string, string?>.Empty
                .Add("MissingMembers", "Green")
                .Add("MissingPatternCount", "1")
                .Add("MissingPattern0", "{ invalid"),
        };
        var provider = new AddMissingArmsCodeFix();
        foreach (var properties in malformed)
        {
            var diagnostic = RoslynDiagnostic.Create(
                descriptor,
                switchExpression.SwitchKeyword.GetLocation(),
                properties);
            Assert.Empty(await GetActionsAsync(document, diagnostic, provider));
        }

        var validProperties = ImmutableDictionary<string, string?>.Empty
            .Add("MissingMembers", "Green")
            .Add("MissingPatternCount", "1")
            .Add("MissingPattern0", "Color.Green");
        var outsideSwitch = RoslynDiagnostic.Create(
            descriptor,
            root.GetLocation(),
            validProperties);
        Assert.Empty(await GetActionsAsync(document, outsideSwitch, provider));
    }

    [Fact]
    public async Task CodeActionTitlesRemainStableForSPIRE009AndSPIRE015()
    {
        const string enumSource = """
            using Houtamelo.Spire;
            [EnforceExhaustiveness]
            enum Color { Red, Green }
            class Consumer
            {
                int Read(Color color) => color switch { Color.Red => 1 };
            }
            """;
        var (enumDocument, enumCompilation) = CreateDocument(enumSource);
        var enumDiagnostic = await GetSingleDiagnosticAsync(
            enumCompilation,
            enumDocument,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());
        var enumActions = await GetActionsAsync(
            enumDocument,
            enumDiagnostic,
            new AddMissingArmsCodeFix());
        Assert.Equal("Add missing enum members", Assert.Single(enumActions).Title);

        const string duSource = """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            partial struct Token
            {
                [Variant] public static partial Token A();
                [Variant] public static partial Token B();
            }
            class Consumer
            {
                int Read(Token token) => token switch { { IsA: true } => 1 };
            }
            """;
        GeneratorTestHelper.RunGenerator(
            duSource,
            out var generatedCompilation,
            out var generatorDiagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        var duDocument = CreateDocumentFromCompilation(generatedCompilation).Document;
        var duDiagnostic = await GetSingleDiagnosticAsync(
            generatedCompilation,
            duDocument,
            new ExhaustivenessAnalyzer());
        var duActions = await GetActionsAsync(
            duDocument,
            duDiagnostic,
            new AddMissingArmsCodeFix());
        Assert.Equal("Add missing variant arms", Assert.Single(duActions).Title);
    }

    [Fact]
    public async Task SPIRE015_FixAll_UpdatesBothSwitchesInOneDocument()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum First { A, B }
            [EnforceExhaustiveness]
            enum Second { X, Y }

            static class Consumer
            {
                public static int One(First value) => value switch
                {
                    First.A => 1,
                };

                public static int Two(Second value) => value switch
                {
                    Second.X => 1,
                };
            }
            """;

        var (document, _) = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var userDiagnostics = diagnostics.Where(d => d.Id == "SPIRE015").ToArray();
        Assert.Equal(2, userDiagnostics.Length);

        var provider = new AddMissingArmsCodeFix();
        var actions = await GetActionsAsync(document, userDiagnostics[0], provider);
        var action = Assert.Single(actions);
        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            action.EquivalenceKey!,
            provider.FixableDiagnosticIds,
            new FixedDiagnosticProvider(userDiagnostics),
            CancellationToken.None);
        var fixAllAction = await fixAll!.GetFixAsync(context);
        Assert.NotNull(fixAllAction);
        var operations = await fixAllAction!.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var fixedDocument = apply.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        Assert.Contains("First.B", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Second.Y", fixedSource, StringComparison.Ordinal);
        AssertNoErrors(CreateCompilation(fixedSource));
    }

    [Fact]
    public async Task FixAll_ProjectScope_UpdatesTwoDocumentsWithoutCrossContamination()
    {
        const string firstSource = """
            using Houtamelo.Spire;
            [EnforceExhaustiveness]
            enum First { Zero, One }
            static class FirstConsumer
            {
                public static int Read(First value) => value switch
                {
                    First.Zero => 0,
                };
            }
            """;
        const string secondSource = """
            using Houtamelo.Spire;
            [EnforceExhaustiveness]
            abstract class Animal { }
            sealed class Dog : Animal { }
            sealed class Cat : Animal { }
            static class SecondConsumer
            {
                public static int Read(Animal value) => value switch
                {
                    Dog => 0,
                };
            }
            """;

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = TestHarnessEnvironment.FrameworkReferences
            .Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location));
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ProjectFixAll", "ProjectFixAll", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, parseOptions)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        foreach (var reference in references)
            solution = solution.AddMetadataReference(projectId, reference);
        var firstId = DocumentId.CreateNewId(projectId);
        var secondId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(
                firstId,
                "first.cs",
                firstSource,
                filePath: "first.cs")
            .AddDocument(
                secondId,
                "second.cs",
                secondSource,
                filePath: "second.cs");
        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync())!;
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer(),
                    new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var userDiagnostics = diagnostics
            .Where(d => d.Id is "SPIRE015" or "SPIRE009")
            .ToArray();
        Assert.Equal(2, userDiagnostics.Length);

        var firstDocument = solution.GetDocument(firstId)!;
        var firstPath = (await firstDocument.GetSyntaxTreeAsync())!.FilePath;
        var firstDiagnostic = Assert.Single(userDiagnostics, d =>
            d.Location.SourceTree?.FilePath == firstPath);
        var provider = new AddMissingArmsCodeFix();
        var actions = await GetActionsAsync(firstDocument, firstDiagnostic, provider);
        var action = Assert.Single(actions);
        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
        var context = new FixAllContext(
            firstDocument,
            provider,
            FixAllScope.Project,
            action.EquivalenceKey!,
            provider.FixableDiagnosticIds,
            new FixedDiagnosticProvider(userDiagnostics),
            CancellationToken.None);
        var fixAllAction = await fixAll!.GetFixAsync(context);
        Assert.NotNull(fixAllAction);
        var operations = await fixAllAction!.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changedProject = apply.ChangedSolution.GetProject(projectId)!;
        var changedCompilation = (await changedProject.GetCompilationAsync())!;
        AssertNoErrors(changedCompilation);
        using var stream = new MemoryStream();
        Assert.True(changedCompilation.Emit(stream).Success);
        var changedDiagnostics = await changedCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer(),
                    new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(
            changedDiagnostics,
            d => d.Id is "SPIRE015" or "SPIRE009");
        var firstText = (await apply.ChangedSolution.GetDocument(firstId)!.GetTextAsync()).ToString();
        var secondText = (await apply.ChangedSolution.GetDocument(secondId)!.GetTextAsync()).ToString();
        Assert.Contains("First.One", firstText, StringComparison.Ordinal);
        Assert.DoesNotContain("Cat", firstText, StringComparison.Ordinal);
        Assert.Contains("Cat", secondText, StringComparison.Ordinal);
        Assert.DoesNotContain("First.One", secondText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlainBoolAndTupleSubjects_DoNotProduceSpire009OrActions()
    {
        const string source = """
            static class Consumer
            {
                public static int Bool(bool value) => value switch
                {
                    true => 1,
                };

                public static int Tuple((bool, bool) value) => value switch
                {
                    (true, true) => 1,
                };
            }
            """;
        var (document, compilation) = CreateDocument(source);
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE009");
        var provider = new AddMissingArmsCodeFix();
        foreach (var diagnostic in diagnostics)
        {
            var actions = await GetActionsAsync(document, diagnostic, provider);
            Assert.Empty(actions);
        }
    }

    [Fact]
    public async Task UnrelatedDiagnostic_HasNoAction()
    {
        const string source = "enum Color { Red, Green }";
        var (document, _) = CreateDocument(source);
        var descriptor = new DiagnosticDescriptor(
            "CS8509",
            "Compiler warning",
            "Incomplete switch",
            "Compiler",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var root = await document.GetSyntaxRootAsync();
        var diagnostic = RoslynDiagnostic.Create(
            descriptor,
            root!.GetLocation());
        var actions = await GetActionsAsync(
            document,
            diagnostic,
            new AddMissingArmsCodeFix());
        Assert.Empty(actions);
    }

    private static async Task<RoslynDiagnostic> GetSingleDiagnosticAsync(
        Compilation compilation,
        Document document,
        DiagnosticAnalyzer analyzer)
    {
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer))
            .GetAnalyzerDiagnosticsAsync();
        var documentPath = (await document.GetSyntaxTreeAsync())!.FilePath;
        return Assert.Single(diagnostics.Where(d =>
            d.Location.SourceTree?.FilePath == documentPath));
    }

    private static async Task<ImmutableArray<RoslynDiagnostic>> AnalyzeWithGlobalOptionAsync(
        Compilation compilation,
        bool enabled)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] =
                enabled ? "true" : "false",
        };
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(options));
        return await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE015ExhaustiveEnumSwitchAnalyzer()),
                new CompilationWithAnalyzersOptions(
                    analyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<string> ApplyFirstFixAsync(
        Document document,
        RoslynDiagnostic diagnostic,
        CodeFixProvider provider)
    {
        var actions = await GetActionsAsync(document, diagnostic, provider);
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changed = apply.ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        Document document,
        RoslynDiagnostic diagnostic,
        CodeFixProvider provider)
    {
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));
        return actions;
    }

    private void ConfigureFormatting(string lineEnding, bool useTabs)
    {
        var options = _workspace.Options
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
        Assert.True(_workspace.TryApplyChanges(
            _workspace.CurrentSolution.WithOptions(options)));
    }

    private (Document Document, Compilation Compilation) CreateDocument(
        string source)
    {
        var compilation = CreateCompilation(source);
        return CreateDocumentFromCompilation(compilation);
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
            "ExactMissingShapeCodeFix",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private (Document Document, Compilation Compilation) CreateDocumentFromCompilation(
        Compilation compilation)
    {
        var projectId = ProjectId.CreateNewId();
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var solution = _workspace.CurrentSolution
            .AddProject(projectId, "ExactMissingShapeCodeFix", "ExactMissingShapeCodeFix", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, parseOptions)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options);
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

    private static Compilation RunGeneratedCompilation(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: "case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        return compilation;
    }

    private static void AssertNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(d =>
                $"{d.Id}: {d.GetMessage()}")));
    }

    private static int CountLiteralNullPatterns(string source)
    {
        return CSharpSyntaxTree.ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<ConstantPatternSyntax>()
            .Count(pattern => pattern.Expression.IsKind(SyntaxKind.NullLiteralExpression));
    }

    private static int CountTokenNullMemberPatterns(string source)
    {
        return CSharpSyntaxTree.ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<ConstantPatternSyntax>()
            .Count(pattern => string.Equals(
                pattern.Expression.ToString(),
                "Token.@null",
                StringComparison.Ordinal));
    }

    private static string GetRawLiteralValue(SyntaxTree tree)
    {
        return tree.GetRoot()
            .DescendantTokens()
            .Single(token => token.Text.Contains("line one", StringComparison.Ordinal))
            .ValueText;
    }

    private sealed class FixedDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<RoslynDiagnostic> _diagnostics;

        public FixedDiagnosticProvider(IEnumerable<RoslynDiagnostic> diagnostics)
            => _diagnostics = diagnostics.ToImmutableArray();

        public override Task<IEnumerable<RoslynDiagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
            => GetDocumentDiagnosticsCoreAsync(document, cancellationToken);

        private async Task<IEnumerable<RoslynDiagnostic>> GetDocumentDiagnosticsCoreAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            var path = tree?.FilePath;
            return _diagnostics.Where(d =>
                path is not null &&
                d.Location.SourceTree?.FilePath == path);
        }

        public override Task<IEnumerable<RoslynDiagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoslynDiagnostic>>(
                ImmutableArray<RoslynDiagnostic>.Empty);

        public override Task<IEnumerable<RoslynDiagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoslynDiagnostic>>(_diagnostics);
    }
}
