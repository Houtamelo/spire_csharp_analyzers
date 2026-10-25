using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.CodeFixes;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.KindEnum;

public sealed class KindEnumTests
{
    [Fact]
    public void GeneratedKindEnums_AreMarkedForExhaustiveness()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            namespace KindAttributeProbe
            {
                // This declaration makes short-name lookup unsafe in generated code.
                public sealed class EnforceExhaustivenessAttribute : System.Attribute { }

                [DiscriminatedUnion]
                public partial struct Plain
                {
                    [Variant] public static partial Plain Idle();
                    [Variant] public static partial Plain Run(int value);
                }

                [DiscriminatedUnion]
                public readonly partial struct ReadonlyPlain
                {
                    [Variant] public static partial ReadonlyPlain Idle();
                    [Variant] public static partial ReadonlyPlain Run(int value);
                }

                [DiscriminatedUnion]
                public partial struct Generic<T>
                {
                    [Variant] public static partial Generic<T> Empty();
                    [Variant] public static partial Generic<T> Value(T value);
                }

                public static partial class Outer
                {
                    [DiscriminatedUnion]
                    public partial struct Nested
                    {
                        [Variant] public static partial Nested First();
                        [Variant] public static partial Nested Second();
                    }
                }
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        foreach (var typeName in new[]
        {
            "KindAttributeProbe.Plain",
            "KindAttributeProbe.ReadonlyPlain",
            "KindAttributeProbe.Generic`1",
            "KindAttributeProbe.Outer+Nested",
        })
        {
            var union = generated.Compilation.GetTypeByMetadataName(typeName);
            Assert.NotNull(union);

            var kind = Assert.Single(union!.GetTypeMembers("Kind"));
            Assert.Contains(
                kind.GetAttributes(),
                attribute => attribute.AttributeClass?.ToDisplayString()
                    == "Houtamelo.Spire.EnforceExhaustivenessAttribute");
        }
    }

    [Fact]
    public async Task IsolatedKindSwitches_AreCheckedWithoutGlobalEnumConfiguration()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Run();
            }

            public static class Consumer
            {
                public static int Expression(Command.Kind kind) => kind switch
                {
                    Command.Kind.Idle => 0,
                };

                public static int Statement(Command.Kind kind)
                {
                    switch (kind)
                    {
                        case Command.Kind.Idle:
                            return 0;
                    }

                    return -1;
                }
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        var diagnostics = await AnalyzeAsync(
            generated.Compilation,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());

        var userDiagnostics = diagnostics
            .Where(d => d.Id == "SPIRE015"
                && d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();

        Assert.Equal(2, userDiagnostics.Count);
    }

    [Fact]
    public async Task ExhaustiveAndCatchallKindSwitches_AreAccepted_AndPlainEnumsRemainOptIn()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Run();
            }

            public enum PlainEnum
            {
                Idle,
                Run,
            }

            public static class Consumer
            {
                public static int Exhaustive(Command.Kind kind) => kind switch
                {
                    Command.Kind.Idle => 0,
                    Command.Kind.Run => 1,
                };

                public static int Catchall(Command.Kind kind) => kind switch
                {
                    Command.Kind.Idle => 0,
                    _ => 1,
                };

                public static int ExhaustiveStatement(Command.Kind kind)
                {
                    switch (kind)
                    {
                        case Command.Kind.Idle:
                            return 0;
                        case Command.Kind.Run:
                            return 1;
                    }

                    return -1;
                }

                public static int CatchallStatement(Command.Kind kind)
                {
                    switch (kind)
                    {
                        case Command.Kind.Idle:
                            return 0;
                        default:
                            return 1;
                    }
                }

                public static int PlainEnumIsOptIn(PlainEnum kind) => kind switch
                {
                    PlainEnum.Idle => 0,
                };
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        var diagnostics = await AnalyzeAsync(
            generated.Compilation,
            new SPIRE015ExhaustiveEnumSwitchAnalyzer());

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == "SPIRE015"
                && diagnostic.Location.SourceTree?.FilePath == "case.cs");
    }

    [Fact]
    public async Task InvalidKindCast_ReportsSPIRE016_AndFromCodeFixProducesValidCode()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Run();
            }

            public static class Consumer
            {
                public static Command.Kind Convert(int raw)
                    => (Command.Kind)raw;
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        using var workspace = CreateWorkspace(generated.Compilation, out var userDocument);
        var analyzer = new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer();
        var initialDiagnostics = await AnalyzeWorkspaceAsync(workspace, analyzer);
        var diagnostic = Assert.Single(initialDiagnostics.Where(
            candidate => candidate.Id == "SPIRE016"
                && candidate.Location.SourceTree?.FilePath == "case.cs"));

        var provider = new FromEnumCodeFix();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            userDocument,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context);
        var action = Assert.Single(actions);

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.Single(operations.OfType<ApplyChangesOperation>());
        applyChanges.Apply(workspace, CancellationToken.None);

        var changedDocument = workspace.CurrentSolution.GetDocument(userDocument.Id)!;
        var changedText = (await changedDocument.GetTextAsync()).ToString();
        Assert.Contains("SpireEnum<Command.Kind>.From(raw)", changedText);

        var changedProject = workspace.CurrentSolution.GetProject(userDocument.Project.Id)!;
        var changedCompilation = (await changedProject.GetCompilationAsync())!;
        AssertNoCompilationErrors(changedCompilation);

        var finalDiagnostics = await AnalyzeWorkspaceAsync(workspace, analyzer);
        Assert.DoesNotContain(
            finalDiagnostics,
            candidate => candidate.Id == "SPIRE016"
                && candidate.Location.SourceTree?.FilePath == "case.cs");
    }

    [Fact]
    public async Task KindConstantValues_AcceptNamedAndValidCast_AndRejectInvalidCast()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Run();
            }

            public static class Consumer
            {
                public static Command.Kind Named => Command.Kind.Idle;
                public static Command.Kind ValidCast => (Command.Kind)0;
                public static Command.Kind InvalidCast => (Command.Kind)99;
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        var diagnostics = await AnalyzeAsync(
            generated.Compilation,
            new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer());

        var invalidDiagnostics = diagnostics
            .Where(d => d.Id == "SPIRE016"
                && d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();
        Assert.Single(invalidDiagnostics);
        Assert.Contains("99", invalidDiagnostics[0].GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InheritedInitializationAttributes_AreHonoredByDefaultAndCastAnalyzers()
    {
        var generated = Generate(
            """
            using System;
            using Houtamelo.Spire;

            [AttributeUsage(AttributeTargets.Enum)]
            public class FirstLevelAttribute : EnforceInitializationAttribute { }

            [AttributeUsage(AttributeTargets.Enum)]
            public sealed class SecondLevelAttribute : FirstLevelAttribute { }

            [EnforceExhaustiveness]
            public enum ExhaustiveNoZero
            {
                One = 1,
                Two = 2,
            }

            [SecondLevel]
            public enum DerivedWithZero
            {
                None = 0,
                One = 1,
            }

            public enum Unmarked
            {
                None = 0,
                One = 1,
            }

            public static class Consumer
            {
                public static ExhaustiveNoZero InvalidDefault => default;
                public static ExhaustiveNoZero Named => ExhaustiveNoZero.One;
                public static ExhaustiveNoZero ValidExhaustiveConstantCast => (ExhaustiveNoZero)1;

                public static DerivedWithZero ValidDefault => default;
                public static DerivedWithZero ValidNamed => DerivedWithZero.One;
                public static DerivedWithZero ValidDerivedConstantCast => (DerivedWithZero)1;
                public static DerivedWithZero InvalidConstantCast => (DerivedWithZero)99;

                public static Unmarked UnmarkedDefault => default;
                public static Unmarked UnmarkedInvalidCast => (Unmarked)99;
            }
            """);

        AssertNoGeneratorDiagnostics(generated.Diagnostics);
        AssertNoCompilationErrors(generated.Compilation);

        var defaultDiagnostics = await AnalyzeAsync(
            generated.Compilation,
            new SPIRE003DefaultOfEnforceInitializationStructAnalyzer());
        var userDefaultDiagnostics = defaultDiagnostics
            .Where(d => d.Id == "SPIRE003"
                && d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();
        Assert.Single(userDefaultDiagnostics);
        Assert.Contains("ExhaustiveNoZero", userDefaultDiagnostics[0].GetMessage(), StringComparison.Ordinal);

        var castDiagnostics = await AnalyzeAsync(
            generated.Compilation,
            new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer());
        var userCastDiagnostics = castDiagnostics
            .Where(d => d.Id == "SPIRE016"
                && d.Location.SourceTree?.FilePath == "case.cs")
            .ToList();
        Assert.Single(userCastDiagnostics);
        Assert.Contains("99", userCastDiagnostics[0].GetMessage(), StringComparison.Ordinal);
    }

    private static GeneratedResult Generate(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: "case.cs");
        return new GeneratedResult(compilation, diagnostics);
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeAsync(
        Compilation compilation,
        params DiagnosticAnalyzer[] analyzers)
    {
        var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray());
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static AdhocWorkspace CreateWorkspace(
        Compilation compilation,
        out Document userDocument)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "KindEnumTests", "KindEnumTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        DocumentId? userDocumentId = null;
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, tree.FilePath, tree.GetText());
            if (tree.FilePath == "case.cs")
                userDocumentId = documentId;
        }

        Assert.NotNull(userDocumentId);
        Assert.True(workspace.TryApplyChanges(solution));
        userDocument = workspace.CurrentSolution.GetDocument(userDocumentId!)!;
        return workspace;
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeWorkspaceAsync(
        AdhocWorkspace workspace,
        DiagnosticAnalyzer analyzer)
    {
        var project = workspace.CurrentSolution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        return await AnalyzeAsync(compilation, analyzer);
    }

    private static void AssertNoGeneratorDiagnostics(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        => GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

    private static void AssertNoCompilationErrors(Compilation compilation)
        => GeneratorTestHelper.AssertNoCompilationErrors(compilation);

    private readonly record struct GeneratedResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics);
}
