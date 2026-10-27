using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

/// Regression coverage for generated Is{Variant} predicates as discriminator guards.
/// Each case runs the real union generator before FieldAccessSafetyAnalyzer.
public sealed class IsVariantGuardTests
{
    [Fact]
    public async Task PositivePredicate_AllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                        return command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PositivePredicate_ReportsWrongAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                        return command.stop.speed;
                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE013", "command.stop", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task NegatedPredicate_WithTwoVariantsNarrowsToTheOtherVariant()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (!command.IsMove)
                        return command.stop.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NegatedPredicate_EarlyExitLeavesMatchingVariant()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (!command.IsMove)
                        return 0;
                    return command.move.speed;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NegatedPredicate_EarlyExitMakesOtherAccessorWrong()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (!command.IsMove)
                        return 0;
                    return command.stop.speed;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE013", "command.stop", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task NegatedPredicate_DoesNotNarrowThreeVariantsToStop()
    {
        var diagnostics = await Analyze(ThreeVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (!command.IsMove)
                        return command.stop.speed;
                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE013", "command.stop", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task EqualsTruePredicate_AllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove == true)
                        return command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task EqualsTruePredicate_ReversedOperandsAllowMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (true == command.IsMove)
                        return command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NotEqualsFalsePredicate_AllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove != false)
                        return command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task EqualsFalsePredicate_AllowsTheComplementAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove == false)
                        return command.stop.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NotEqualsTruePredicate_AllowsTheComplementAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove != true)
                        return command.stop.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task EqualsFalsePredicate_EarlyExitLeavesMatchingVariant()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove == false)
                        return 0;
                    return command.move.speed;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NotEqualsTruePredicate_EarlyExitLeavesMatchingVariant()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove != true)
                        return 0;
                    return command.move.speed;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PositivePredicateElseBranchAllowsTheComplementAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                        return command.move.speed;
                    else
                        return command.stop.speed;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PositivePredicateElseBranchDoesNotNarrowThreeVariants()
    {
        var diagnostics = await Analyze(ThreeVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                        return command.move.speed;
                    else
                        return command.stop.speed;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE013", "command.stop", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task PositivePropertyPattern_AllowsMatchingPayload()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: true, move: var move } => move.speed,
                    _ => 0,
                };
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PositivePropertyPattern_ReportsWrongPayload()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: true, stop: var stop } => stop.speed,
                    _ => 0,
                };
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE013", "stop", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task RestrictedPositivePropertyPattern_DoesNotProveThePostGuardComplement()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command is { IsMove: true, move: { speed: 0 } })
                        return 0;
                    return command.stop.speed;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "command.stop", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task RestrictedPositivePropertyPattern_DoesNotProveTheElseComplement()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command is { IsMove: true, move: { speed: 0 } })
                        return 0;
                    else
                        return command.stop.speed;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "command.stop", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task NegativePropertyPattern_WithTwoVariantsAllowsComplementPayload()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command) => command switch
                {
                    { IsMove: false, stop: var stop } => stop.speed,
                    _ => 0,
                };
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PredicateGuardOnAnotherReceiverDoesNotTransfer()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command first, Command second)
                {
                    if (first.IsMove)
                        return second.move.speed;
                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "second.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PredicateGuardOnDifferentHolderInstanceDoesNotTransfer()
    {
        var diagnostics = await Analyze(HolderSource("""
            static class Reader
            {
                public static int Read(Holder first, Holder second)
                {
                    if (first.command.IsMove)
                        return second.command.move.speed;
                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "second.command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PredicateGuardOnSameHolderInstanceAllowsAccessor()
    {
        var diagnostics = await Analyze(HolderSource("""
            static class Reader
            {
                public static int Read(Holder holder)
                {
                    if (holder.command.IsMove)
                        return holder.command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task ThisCommandPredicateGuardStillAllowsAccessor()
    {
        var diagnostics = await Analyze(HolderSource("""
            sealed class Reader
            {
                private Command command;

                public int Read()
                {
                    if (this.command.IsMove)
                        return this.command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task ChangingCommandPropertyDoesNotTransferPredicateProof()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);
            }

            sealed class Holder
            {
                private int reads;

                public Command command
                {
                    get
                    {
                        reads++;
                        return reads == 1 ? Command.Move(1) : Command.Stop(2);
                    }
                }
            }

            static class Reader
            {
                public static int Read(Holder holder)
                {
                    if (holder.command.IsMove)
                        return holder.command.move.speed;
                    return 0;
                }
            }
            """);

        AssertDiagnostic(diagnostics, "SPIRE014", "holder.command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task ChangingReceiverPropertyDoesNotTransferPredicateProof()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);
            }

            sealed class Holder
            {
                private int reads;
                public Command command;

                public Holder Next
                {
                    get
                    {
                        reads++;
                        return new Holder
                        {
                            command = reads == 1 ? Command.Move(1) : Command.Stop(2),
                        };
                    }
                }
            }

            static class Reader
            {
                public static int Read(Holder holder)
                {
                    if (holder.Next.command.IsMove)
                        return holder.Next.command.move.speed;
                    return 0;
                }
            }
            """);

        AssertDiagnostic(diagnostics, "SPIRE014", "holder.Next.command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PredicateGuardInvalidatedByAssignmentDoesNotTransfer()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command, Command other)
                {
                    if (command.IsMove)
                    {
                        command = other;
                        return command.move.speed;
                    }

                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PredicateGuardInsideLambdaAllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    System.Func<int> read = () =>
                    {
                        if (command.IsMove)
                            return command.move.speed;
                        return 0;
                    };
                    return read();
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task UnguardedLambdaStillReportsUnguardedAccess()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    System.Func<int> read = () => command.move.speed;
                    return read();
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task OuterPredicateGuardDoesNotLeakIntoNestedLambda()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                    {
                        System.Func<int> read = () => command.move.speed;
                        return read();
                    }

                    return 0;
                }
            }
            """));

        AssertDiagnostic(diagnostics, "SPIRE014", "command.move", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PredicateGuardInsideLocalFunctionAllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    return ReadMove(command);

                    static int ReadMove(Command value)
                    {
                        if (value.IsMove)
                            return value.move.speed;
                        return 0;
                    }
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PredicateGuardInsideAsyncMethodAllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static async System.Threading.Tasks.Task<int> Read(Command command)
                {
                    await System.Threading.Tasks.Task.Yield();
                    if (command.IsMove)
                        return command.move.speed;
                    return 0;
                }
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PredicateGuardInTernaryAllowsMatchingAccessor()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                    => command.IsMove ? command.move.speed : 0;
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task PredicateGuardAllowsPayloadAsMethodArgument()
    {
        var diagnostics = await Analyze(TwoVariantSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsMove)
                        return Consume(command.move.speed);
                    return 0;
                }

                private static int Consume(int value) => value;
            }
            """));

        AssertNoVariantDiagnostics(diagnostics);
    }

    [Fact]
    public async Task UnrelatedBooleanPropertyDoesNotEstablishVariantGuard()
    {
        var diagnostics = await Analyze("""
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);

                public bool IsFoo => true;
            }

            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.IsFoo)
                        return command.move.speed;
                    return 0;
                }
            }
            """);

        AssertDiagnostic(diagnostics, "SPIRE014", "command.move", DiagnosticSeverity.Warning);
    }

    private static void AssertNoVariantDiagnostics(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    private static void AssertDiagnostic(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string id,
        string sourceText,
        DiagnosticSeverity severity)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.NotNull(diagnostic.Location.SourceTree);
        Assert.Equal(
            sourceText,
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    private static string TwoVariantSource(string consumer)
        => """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);
            }
            """ + consumer;

    private static string ThreeVariantSource(string consumer)
        => """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);
                [Variant] public static partial Command Idle();
            }
            """ + consumer;

    private static string HolderSource(string consumer)
        => """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop(int speed);
            }

            sealed class Holder
            {
                public Command command;
            }
            """ + consumer;

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(
        string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "is-variant-guard.cs");

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
