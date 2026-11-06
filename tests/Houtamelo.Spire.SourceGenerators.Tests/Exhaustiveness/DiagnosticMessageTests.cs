using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

/// Verifies the user-facing SPIRE009 title, description, and message contract.
public sealed class DiagnosticMessageTests
{
    private const string TypeTitle =
        "Switch does not handle all possible cases of type marked with `[EnforceExhaustiveness]`";

    private const string InterfaceTitle =
        "Switch does not handle all possible cases of interface marked with `[EnforceExhaustiveness]`";

    [Fact]
    public async Task EnforceRecord_UsesGeneralizedTitleAndCaseMessage()
    {
        var diagnostic = await Analyze(EnforceRecordSource());

        AssertContract(
            diagnostic,
            TypeTitle,
            "Switch on 'BaseState' does not handle case(s): 'IdleState', 'MovingState', 'SpeakingState'");
    }

    [Fact]
    public async Task EnforceClass_UsesGeneralizedTitleForSwitchExpression()
    {
        var diagnostic = await Analyze("""
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public abstract class BaseState { }

            public sealed class IdleState : BaseState { }
            public sealed class MovingState : BaseState { }

            public static class Consumer
            {
                public static int Read(BaseState state) => state switch
                {
                    IdleState => 0,
                };
            }
            """);

        AssertContract(
            diagnostic,
            TypeTitle,
            "Switch on 'BaseState' does not handle case(s): 'MovingState'");
    }

    [Fact]
    public async Task EnforceInterface_UsesInterfaceSpecificTitle()
    {
        var diagnostic = await Analyze("""
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public interface IBaseState { }

            public sealed class MovingState : IBaseState { }
            public sealed class IdleState : IBaseState { }

            public static class Consumer
            {
                public static void Read(IBaseState state)
                {
                    switch (state)
                    {
                    }
                }
            }
            """);

        AssertContract(
            diagnostic,
            InterfaceTitle,
            "Switch on 'IBaseState' does not handle case(s): 'IdleState', 'MovingState'");
    }

    [Fact]
    public async Task EnforceGenericRecord_UsesTypeTitle()
    {
        var diagnostic = await Analyze("""
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public abstract record BaseState<T> { }

            public sealed record IdleState<T> : BaseState<T> { }
            public sealed record MovingState<T> : BaseState<T> { }

            public static class Consumer
            {
                public static int Read(BaseState<int> state) => state switch
                {
                    IdleState<int> => 0,
                };
            }
            """);

        AssertContract(
            diagnostic,
            TypeTitle,
            "Switch on 'BaseState' does not handle case(s): 'MovingState'");
    }

    [Fact]
    public async Task GeneratedStructUnion_UsesCaseMessage()
    {
        var diagnostic = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }

            public static class Consumer
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: true, move: var (destination) } => destination,
                    { IsStop: true } => 0,
                };
            }
            """);

        AssertContract(
            diagnostic,
            TypeTitle,
            "Switch on 'Command' does not handle case(s): 'Idle'");
    }

    [Fact]
    public async Task GeneratedRecordUnion_UsesCaseMessage()
    {
        var diagnostic = await AnalyzeGenerated("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public abstract partial record Result
            {
                public sealed partial record Ok(int Value) : Result;
                public sealed partial record Error(string Message) : Result;
            }

            public static class Consumer
            {
                public static int Read(Result result) => result switch
                {
                    Result.Ok ok => ok.Value,
                    null => -1,
                };
            }
            """);

        AssertContract(
            diagnostic,
            TypeTitle,
            "Switch on 'Result' does not handle case(s): 'Error'");
    }

    [Fact]
    public async Task CompleteEnforceHierarchy_HasNoDiagnostic()
    {
        var diagnostics = await AnalyzeAll("""
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public abstract record BaseState { }

            public sealed record IdleState : BaseState { }
            public sealed record MovingState : BaseState { }

            public static class Consumer
            {
                public static void Read(BaseState state)
                {
                    switch (state)
                    {
                        case IdleState:
                            break;
                        case MovingState:
                            break;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task CompleteEnforceInterface_HasNoDiagnostic()
    {
        var diagnostics = await AnalyzeAll("""
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public interface IBaseState { }

            public sealed class IdleState : IBaseState { }
            public sealed class MovingState : IBaseState { }

            public static class Consumer
            {
                public static void Read(IBaseState state)
                {
                    switch (state)
                    {
                        case IdleState:
                            break;
                        case MovingState:
                            break;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task EnforceInterface_SwitchExpression_UsesInterfaceSpecificTitle()
    {
        var diagnostic = await Analyze("""
            #nullable enable
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            public interface IBaseState { }

            public sealed class IdleState : IBaseState { }
            public sealed class MovingState : IBaseState { }

            public static class Consumer
            {
                public static int Read(IBaseState? state) => state switch
                {
                    IdleState => 0,
                    null => -1,
                };
            }
            """);

        AssertContract(
            diagnostic,
            InterfaceTitle,
            "Switch on 'IBaseState' does not handle case(s): 'MovingState'");
    }

    [Fact]
    public async Task UnannotatedHierarchy_HasNoDiagnostic()
    {
        var diagnostics = await AnalyzeAll("""
            public abstract record BaseState { }
            public sealed record IdleState : BaseState { }
            public sealed record MovingState : BaseState { }

            public static class Consumer
            {
                public static void Read(BaseState state)
                {
                    switch (state)
                    {
                        case IdleState:
                            break;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    private static void AssertContract(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string expectedTitle,
        string expectedMessage)
    {
        AssertCommonContract(diagnostic, expectedTitle);
        Assert.Equal(expectedMessage, diagnostic.GetMessage());
    }

    private static void AssertCommonContract(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string expectedTitle)
    {
        Assert.Equal("SPIRE009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(expectedTitle, diagnostic.Descriptor.Title.ToString());
        Assert.Contains(
            "all possible cases",
            diagnostic.Descriptor.Description.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "[EnforceExhaustiveness]",
            diagnostic.Descriptor.Description.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "discriminated union",
            diagnostic.Descriptor.Description.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("variant(s)", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal("pending-diagnostic.cs", diagnostic.Location.SourceTree?.FilePath);
    }

    private static async Task<Microsoft.CodeAnalysis.Diagnostic> Analyze(string source)
    {
        var diagnostics = await AnalyzeAll(source);
        return Assert.Single(diagnostics);
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeAll(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id is not "SPIRE009");
        return diagnostics
            .Where(diagnostic => diagnostic.Id == "SPIRE009")
            .ToImmutableArray();
    }

    private static async Task<Microsoft.CodeAnalysis.Diagnostic> AnalyzeGenerated(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: "pending-diagnostic.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id is not "SPIRE009");
        return Assert.Single(diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE009"));
    }

    private static Compilation CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "pending-diagnostic.cs");
        var references = TestHarnessEnvironment.FrameworkReferences
            .Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location));
        return CSharpCompilation.Create(
            "PendingDiagnosticTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static string EnforceRecordSource() => """
        using Houtamelo.Spire;

        [EnforceExhaustiveness]
        public abstract record BaseState { }

        public sealed record SpeakingState : BaseState { }
        public sealed record IdleState : BaseState { }
        public sealed record MovingState : BaseState { }

        public static class Consumer
        {
            public static void Read(BaseState state)
            {
                switch (state)
                {
                }
            }
        }
        """;
}
