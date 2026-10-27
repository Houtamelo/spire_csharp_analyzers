using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class SingleValueCaseRegressionTests
{
    [Fact]
    public async Task SingleValueCase_GuardsMatchingField()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
            {
                [Variant] public static partial Command Move(int destination);
                [Variant] public static partial Command Rotate(float angle);
            }

            static class Reader
            {
                public static int Read(Command command)
                {
                    switch (command.kind)
                    {
                        case Command.Kind.Move:
                            return command.move.destination;
                        default:
                            return 0;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MultipleSingleValueLabels_GuardSharedField()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SingleValueCase_WrongVariantDiagnosticContract()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
            {
                [Variant] public static partial Command Move(int destination);
                [Variant] public static partial Command ApplyEffect(int effect);
            }

            static class Reader
            {
                public static int Read(Command command)
                {
                    switch (command.kind)
                    {
                        case Command.Kind.Move:
                            return command.applyEffect.effect;
                        default:
                            return 0;
                    }
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIRE013", diagnostic.Id);
        Assert.Equal(
            "Attempting to access mismatched variant data",
            diagnostic.Descriptor.Title.ToString());
        Assert.Equal(
            "The field 'applyEffect' only contains valid data when 'kind=ApplyEffect', " +
                "but within this scope, it is statically known that 'kind=Move'.\n" +
                "Help: Within this scope, only the field 'move' contains valid data.",
            diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("single-value-case.cs", diagnostic.Location.SourceTree?.FilePath);
        Assert.Equal(
            "command.applyEffect",
            diagnostic.Location.SourceTree?.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task UnguardedFieldAccess_ReportsSPIRE014()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            partial struct Command
            {
                [Variant] public static partial Command Move(int destination);
                [Variant] public static partial Command Rotate(float angle);
            }

            static class Reader
            {
                public static int Read(Command command)
                    => command.move.destination;
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIRE014", diagnostic.Id);
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "single-value-case.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new FieldAccessSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return analyzerDiagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
            .ToImmutableArray();
    }
}
