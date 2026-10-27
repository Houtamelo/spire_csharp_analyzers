using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class DiagnosticMessageTests
{
    [Fact]
    public async Task FieldAccess_WrongVariantDiagnosticContract()
    {
        var source = CommandSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        return command.applyEffect.effect;
                    return 0;
                }
            }
            """);

        var diagnostics = await Analyze(source);

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE013",
            "Attempting to access mismatched variant data",
            "The field 'applyEffect' only contains valid data when 'kind=ApplyEffect', " +
                "but within this scope, it is statically known that 'kind=Move'.\n" +
                "Help: Within this scope, only the field 'move' contains valid data.",
            DiagnosticSeverity.Error,
            "command.applyEffect",
            "variant-payload-consumer.cs");
    }

    [Fact]
    public async Task FieldAccess_MultipleGuardedKindsNamesAreDeterministic()
    {
        var source = ThreeVariantCommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind != Command.Kind.Idle)
                        return command.move.speed;
                    return 0f;
                }
            }
            """);

        var diagnostics = await Analyze(source);

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE013",
            "Attempting to access mismatched variant data",
            "The field 'move' only contains valid data when 'kind=Move', " +
                "but within this scope, it is statically known that 'kind=Move' or 'kind=Stop'.\n" +
                "Help: Check 'kind' again before accessing variant data; this scope can contain more than one variant.",
            DiagnosticSeverity.Error,
            "command.move",
            "variant-payload-consumer.cs");
    }

    [Fact]
    public async Task FieldAccess_GroupedKindsNamesAreDeterministic()
    {
        var source = ThreeVariantCommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    switch (command.kind)
                    {
                        case Command.Kind.Move:
                        case Command.Kind.Stop:
                            return command.move.speed;
                        default:
                            return 0f;
                    }
                }
            }
            """);

        var diagnostics = await Analyze(source);

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE013",
            "Attempting to access mismatched variant data",
            "The field 'move' only contains valid data when 'kind=Move', " +
                "but within this scope, it is statically known that 'kind=Move' or 'kind=Stop'.\n" +
                "Help: Check 'kind' again before accessing variant data; this scope can contain more than one variant.",
            DiagnosticSeverity.Error,
            "command.move",
            "variant-payload-consumer.cs");
    }

    [Fact]
    public async Task FieldAccess_UnguardedDiagnosticContract()
    {
        var source = CommandSource("""
            static class Reader
            {
                public static int Read(Command command)
                    => command.applyEffect.effect;
            }
            """);

        var diagnostics = await Analyze(source);

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE014",
            "Attempting to access variant data without checking 'kind'",
            "The field 'applyEffect' only contains valid data when 'kind=ApplyEffect', " +
                "but within this scope, the value of 'kind' is unknown.\n" +
                "Help 1: Check if 'kind=ApplyEffect' before accessing the field 'applyEffect'.\n" +
                "Help 2: If you are absolutely sure that 'kind=ApplyEffect', you can suppress this diagnostic with `#pragma warning disable SPIRE014`.",
            DiagnosticSeverity.Warning,
            "command.applyEffect",
            "variant-payload-consumer.cs");
    }

    [Fact]
    public async Task FieldAccess_FactoryConflictAccessorUsesQualifiedNameInDiagnostic()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Naming
            {
                [Variant] public static partial Naming run();
                [Variant] public static partial Naming HTTPRequest(string value);
            }

            static class Reader
            {
                public static NamingTypes.Variants.run Read(Naming naming)
                    => naming.run_value;
            }
            """;

        var diagnostics = await Analyze(source, "field-collision.cs");

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE014",
            "Attempting to access variant data without checking 'kind'",
            "The field 'run_value' only contains valid data when 'kind=run', " +
                "but within this scope, the value of 'kind' is unknown.\n" +
                "Help 1: Check if 'kind=run' before accessing the field 'run_value'.\n" +
                "Help 2: If you are absolutely sure that 'kind=run', you can suppress this diagnostic with `#pragma warning disable SPIRE014`.",
            DiagnosticSeverity.Warning,
            "naming.run_value",
            "field-collision.cs");
    }

    [Fact]
    public async Task FieldAccess_FactoryConflictAccessorWrongVariantUsesQualifiedNameInHelp()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Naming
            {
                [Variant] public static partial Naming run();
                [Variant] public static partial Naming HTTPRequest(string value);
            }

            static class Reader
            {
                public static NamingTypes.Variants.run Read(Naming naming)
                {
                    if (naming.kind == Naming.Kind.HTTPRequest)
                        return naming.run_value;
                    return default;
                }
            }
            """;

        var diagnostics = await Analyze(source, "field-collision.cs");

        var diagnostic = Assert.Single(diagnostics);
        AssertDiagnosticContract(
            diagnostic,
            "SPIRE013",
            "Attempting to access mismatched variant data",
            "The field 'run_value' only contains valid data when 'kind=run', " +
                "but within this scope, it is statically known that 'kind=HTTPRequest'.\n" +
                "Help: Within this scope, only the field 'httpRequest' contains valid data.",
            DiagnosticSeverity.Error,
            "naming.run_value",
            "field-collision.cs");
    }

    [Fact]
    public async Task FieldAccess_MatchingKindGuard_NoDiagnostic()
    {
        var source = CommandSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        return command.move.destination;
                    return 0;
                }
            }
            """);

        var diagnostics = await Analyze(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FieldAccess_SharedPayloadFieldNamesRemainOrdinary()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination);
                [Variant] public static partial Command Rotate(int destination);
            }

            static class Reader
            {
                public static int Read(Command command)
                {
                    switch (command.kind)
                    {
                        case Command.Kind.Move:
                            return command.move.destination;
                        case Command.Kind.Rotate:
                            return command.rotate.destination;
                        default:
                            return 0;
                    }
                }
            }
            """;

        var diagnostics = await Analyze(source, "single-value-case.cs");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FieldAccess_MatchingPropertyPattern_NoDiagnostic()
    {
        var source = CommandSource("""
            static class Reader
            {
                public static bool IsFast(Command command)
                    => command is
                        { kind: Command.Kind.Move, move: { speed: var speed } }
                        && speed > 1f;
            }
            """);

        var diagnostics = await Analyze(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FieldAccess_FactoryConflictAccessorGuardedAndFactoryRead_NoDiagnostic()
    {
        var source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Naming
            {
                [Variant] public static partial Naming run();
                [Variant] public static partial Naming HTTPRequest(string value);
            }

            static class Reader
            {
                public static NamingTypes.Variants.run Read(Naming naming)
                {
                    if (naming.kind == Naming.Kind.run)
                        return naming.run_value;
                    return Naming.run().run_value;
                }
            }
            """;

        var diagnostics = await Analyze(source, "field-collision.cs");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FieldAccess_PragmaSuppressionSuppressesUnguardedAccess()
    {
        var source = CommandSource("""
            static class Reader
            {
                #pragma warning disable SPIRE014
                public static int Read(Command command)
                    => command.applyEffect.effect;
                #pragma warning restore SPIRE014
            }
            """);

        var diagnostics = await Analyze(source);

        Assert.Empty(diagnostics);
    }

    private static void AssertDiagnosticContract(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string id,
        string title,
        string message,
        DiagnosticSeverity severity,
        string accessor,
        string sourcePath)
    {
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(title, diagnostic.Descriptor.Title.ToString());
        Assert.Equal(message, diagnostic.GetMessage());
        Assert.Equal(severity, diagnostic.Severity);

        var sourceTree = diagnostic.Location.SourceTree;
        Assert.NotNull(sourceTree);
        Assert.Equal(sourcePath, sourceTree!.FilePath);
        Assert.Equal(
            accessor,
            sourceTree.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    private static string CommandSource(string consumer)
        => """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command ApplyEffect(int effect);
            }
            """ + consumer;

    private static string ThreeVariantCommandSource(string consumer)
        => """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }
            """ + consumer;

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(
        string source,
        string path = "variant-payload-consumer.cs")
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: path);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var diagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new FieldAccessSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
            .ToImmutableArray();
    }
}
