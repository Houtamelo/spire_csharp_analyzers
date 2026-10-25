using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.VariantPayloadConsumers;

/// Phase-two analyzer contract for the sibling payload/accessor representation.
/// Every case runs the real union generator before the selected analyzer.
public sealed class VariantPayloadAnalyzerTests
{
    [Fact]
    public async Task Exhaustiveness_MissingFieldlessVariantThroughQualifiedPayload()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static int Read(Command command) => command switch
                    {
                        { kind: Command.Kind.Move, move: { speed: var speed } } => (int)speed,
                    };
                }
                """),
            new ExhaustivenessAnalyzer(), new CS8509Suppressor());

        AssertSingle(diagnostics, "SPIRE009");
    }

    [Fact]
    public async Task Exhaustiveness_FullQualifiedPayloadPatternsPass()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command) => command switch
                    {
                        { kind: Command.Kind.Move, move: { destination: var destination, speed: var speed } } => speed,
                        { kind: Command.Kind.Stop, stop: var _ } => 0f,
                    };
                }
                """),
            new ExhaustivenessAnalyzer(), new CS8509Suppressor());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
    }

    [Fact]
    public async Task Exhaustiveness_GuardedPayloadArmDoesNotCoverTheVariant()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static int Read(Command command) => command switch
                    {
                        { kind: Command.Kind.Move, move: { speed: var speed } } when speed > 0 => 1,
                        { kind: Command.Kind.Stop, stop: var _ } => 0,
                    };
                }
                """),
            new ExhaustivenessAnalyzer(), new CS8509Suppressor());

        AssertSingle(diagnostics, "SPIRE009");
    }

    [Fact]
    public async Task Exhaustiveness_GenericPayloadUsesClosedAccessor()
    {
        var diagnostics = await Analyze(
            """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Option<T>
            {
                [Variant] public static partial Option<T> Some(T value);
                [Variant] public static partial Option<T> None();
            }
            static class Reader
            {
                public static int Read(Option<int> option) => option switch
                {
                    { kind: Option<int>.Kind.Some, some: { value: var value } } => value,
                };
            }
            """,
            new ExhaustivenessAnalyzer(), new CS8509Suppressor());

        AssertSingle(diagnostics, "SPIRE009");
    }

    [Fact]
    public async Task Exhaustiveness_RecordControlRemainsSupported()
    {
        var diagnostics = await Analyze(
            """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public abstract partial record Result
            {
                public sealed partial record Ok(int Value) : Result;
                public sealed partial record Error(string Message) : Result;
            }
            static class Reader
            {
                public static int Read(Result result) => result switch
                {
                    Result.Ok ok => ok.Value,
                };
            }
            """,
            new ExhaustivenessAnalyzer(), new CS8509Suppressor());

        AssertSingle(diagnostics, "SPIRE009");
    }

    [Fact]
    public async Task FieldAccess_UnguardedQualifiedAccessorReportsSPIRE014()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command) => command.move.speed;
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_MatchingKindGuardAllowsTheAccessor()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command)
                    {
                        if (command.kind == Command.Kind.Move)
                            return command.move.speed;
                        return 0f;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_WrongAccessorInsideKindGuardReportsSPIRE013()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static CommandTypes.Variants.Stop Read(Command command)
                    {
                        if (command.kind == Command.Kind.Move)
                            return command.stop;
                        return Command.Stop().stop;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE013");
    }

    [Fact]
    public async Task FieldAccess_DetachedPayloadIsAnOrdinaryStruct()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command)
                    {
                        if (command.kind != Command.Kind.Move)
                            return 0f;
                        var payload = command.move;
                        return payload.speed;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_MutationInvalidatesTheEarlierGuard()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command, Command other)
                    {
                        if (command.kind == Command.Kind.Move)
                        {
                            command = other;
                            return command.move.speed;
                        }
                        return 0f;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_ConditionalExitDoesNotProveTheRemainingPath()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command, bool flag)
                    {
                        if (command.kind != Command.Kind.Move)
                        {
                            if (flag)
                                return 0f;
                        }

                        return command.move.speed;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_ReassignmentAfterEarlyExitInvalidatesProof()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command, Command other)
                    {
                        if (command.kind != Command.Kind.Move)
                            return 0f;

                        command = other;
                        return command.move.speed;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_SwitchGuardDoesNotTransferToAnotherInstance()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command first, Command second)
                    {
                        switch (first.kind)
                        {
                            case Command.Kind.Move:
                                return second.move.speed;
                            default:
                                return 0f;
                        }
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_SwitchExpressionGuardDoesNotTransferToAnotherInstance()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command first, Command second) => first switch
                    {
                        { kind: Command.Kind.Move } => second.move.speed,
                        _ => 0f,
                    };
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_ClosureReturnDoesNotExitTheContainingMethod()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command)
                    {
                        if (command.kind != Command.Kind.Move)
                        {
                            System.Action action = () =>
                            {
                                return;
                            };
                        }

                        return command.move.speed;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task TypeSafety_PartialNestedPropertyPatternIsValid()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static bool IsFast(Command command) => command is
                        { kind: Command.Kind.Move, move: { speed: var speed } } && speed > 1f;
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_CompatibleNestedExplicitTypeIsValid()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static bool IsFast(Command command) => command is
                        { kind: Command.Kind.Move, move: { speed: float speed } } && speed > 1f;
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_NestedPayloadPositionalTypesAreChecked()
    {
        var diagnostics = await AnalyzeAllowingCompilationErrors(
            CommandSource("""
                static class Reader
                {
                    public static bool IsFast(Command command) => command is
                        { kind: Command.Kind.Move, move: (string destination, float speed) };
                }
                """),
            new TypeSafetyAnalyzer());

        var diagnostic = AssertSingle(diagnostics, "SPIRE011");
        Assert.Equal("int", diagnostic.Properties["ExpectedType"]);
    }

    [Fact]
    public async Task TypeSafety_PayloadDeconstructUsesOnlyPayloadFields()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static bool IsStop(Command command) => command is
                        { kind: Command.Kind.Stop, stop: CommandTypes.Variants.Stop() };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_FieldlessVariantDoesNotInventPayloadFields()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static bool IsStop(Command command) => command is
                        { kind: Command.Kind.Stop, stop: var _ };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task FieldAccess_WrongAccessorInsidePropertyPatternReportsSPIRE013()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static CommandTypes.Variants.Stop Read(Command command) => command switch
                    {
                        { kind: Command.Kind.Move, stop: var stop } => stop,
                        _ => Command.Stop().stop,
                    };
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE013");
    }

    [Fact]
    public async Task FieldAccess_NegatedKindGuardWithMultiplePossibleVariantsReportsSPIRE013()
    {
        var diagnostics = await Analyze(
            ThreeVariantCommandSource("""
                static class Reader
                {
                    public static float Read(Command command)
                    {
                        if (command.kind != Command.Kind.Idle)
                            return command.move.speed;
                        return 0f;
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE013");
    }

    [Fact]
    public async Task FieldAccess_GroupedSwitchKindsWithNonOwnerReportsSPIRE013()
    {
        var diagnostics = await Analyze(
            ThreeVariantCommandSource("""
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
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE013");
    }

    [Fact]
    public async Task FieldAccess_OrKindPropertyPatternWithNonOwnerReportsSPIRE013()
    {
        var diagnostics = await Analyze(
            ThreeVariantCommandSource("""
                static class Reader
                {
                    public static float Read(Command command) => command switch
                    {
                        { kind: Command.Kind.Move or Command.Kind.Stop, move: var payload } => payload.speed,
                        _ => 0f,
                    };
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE013");
    }

    [Fact]
    public async Task FieldAccess_MatchingAccessorInsideKindSwitchIsValid()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static CommandTypes.Variants.Move Read(Command command)
                    {
                        switch (command.kind)
                        {
                            case Command.Kind.Move:
                                return command.move;
                            default:
                                return Command.Move(0, 0f).move;
                        }
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_FactoryConflictAccessorUsesRunValue()
    {
        var diagnostics = await Analyze(
            """
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
            """,
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_FactoryConflictAccessorWithoutGuardReportsSPIRE014()
    {
        var diagnostics = await Analyze(
            """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Naming
            {
                [Variant] public static partial Naming run();
                [Variant] public static partial Naming HTTPRequest(string value);
            }
            static class Reader
            {
                public static NamingTypes.Variants.run Read(Naming naming) => naming.run_value;
            }
            """,
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_LambdaWithKindProofIsValid()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static System.Func<Command, float> Create()
                        => command =>
                        {
                            if (command.kind == Command.Kind.Move)
                                return command.move.speed;
                            return 0f;
                        };
                }
                """),
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_LocalFunctionWithKindProofIsValid()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static float Read(Command command)
                    {
                        return Local(command);

                        static float Local(Command value)
                        {
                            if (value.kind == Command.Kind.Move)
                                return value.move.speed;
                            return 0f;
                        }
                    }
                }
                """),
            new FieldAccessSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014");
    }

    [Fact]
    public async Task FieldAccess_UnguardedLambdaReportsSPIRE014()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static System.Func<Command, float> Create()
                        => command => command.move.speed;
                }
                """),
            new FieldAccessSafetyAnalyzer());

        AssertSingle(diagnostics, "SPIRE014");
    }

    [Fact]
    public async Task TypeSafety_NestedPropertyTypeMismatchReportsSPIRE011()
    {
        var diagnostics = await AnalyzeAllowingCompilationErrors(
            CommandSource("""
                static class Reader
                {
                    public static bool IsFast(Command command) => command is
                        { kind: Command.Kind.Move, move: { speed: string speed } };
                }
                """),
            new TypeSafetyAnalyzer());

        var diagnostic = AssertSingle(diagnostics, "SPIRE011");
        Assert.Equal("var", diagnostic.Properties["ExpectedType"]);
    }

    [Fact]
    public async Task TypeSafety_ObjectPatternRemainsBoxingCompatible()
    {
        var diagnostics = await Analyze(
            CommandSource("""
                static class Reader
                {
                    public static bool IsFast(Command command) => command is
                        { kind: Command.Kind.Move, move: { speed: object value } };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_NullableReferenceNonNullFilterIsValid()
    {
        var diagnostics = await Analyze(
            """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct OptionalCommand
            {
                [Variant] public static partial OptionalCommand Optional(string? text);
                [Variant] public static partial OptionalCommand Empty();
            }
            static class Reader
            {
                public static bool HasText(OptionalCommand command) => command is
                    { kind: OptionalCommand.Kind.Optional, optional: { text: string text } };
            }
            """,
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_NestedOrdinaryStructPropertyUsesItsOwnType()
    {
        var diagnostics = await Analyze(
            NestedOrdinaryStructSource("""
                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, move: { foo: { value: string text } } };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_CustomUnionPropertyReturningOrdinaryStructIsIgnored()
    {
        var diagnostics = await Analyze(
            NestedOrdinaryStructSource("""
                public partial struct Command
                {
                    public Foo custom => default;
                }

                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, custom: { value: string text } };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_DetachedNestedOrdinaryStructRemainsOrdinaryCode()
    {
        var diagnostics = await Analyze(
            NestedOrdinaryStructSource("""
                static class Reader
                {
                    public static string Read(Command command)
                    {
                        if (command.kind == Command.Kind.Move)
                        {
                            var foo = command.move.foo;
                            return foo.value;
                        }

                        return string.Empty;
                    }
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_NestedPayloadPositionalExactTypesAreValid()
    {
        var diagnostics = await Analyze(
            NestedOrdinaryStructSource("""
                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, move: (Foo foo, int value) };
                }
                """),
            new TypeSafetyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "SPIRE011" or "SPIRE012");
    }

    [Fact]
    public async Task TypeSafety_PositionalMismatchReportsTheExpectedFieldType()
    {
        var diagnostics = await AnalyzeAllowingCompilationErrors(
            NestedOrdinaryStructSource("""
                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, move: (string foo, int value) };
                }
                """),
            new TypeSafetyAnalyzer());

        var diagnostic = AssertSingle(diagnostics, "SPIRE011");
        Assert.Equal("global::Foo", diagnostic.Properties["ExpectedType"]);
    }

    [Fact]
    public async Task TypeSafety_PositionalSecondPayloadFieldReportsItsOwnIndex()
    {
        var diagnostics = await AnalyzeAllowingCompilationErrors(
            CommandSource("""
                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, move: (int destination, string speed) };
                }
                """),
            new TypeSafetyAnalyzer());

        var diagnostic = AssertSingle(diagnostics, "SPIRE011");
        Assert.Equal("1", diagnostic.Properties["FieldIndex"]);
    }

    [Fact]
    public async Task TypeSafety_PropertyMismatchUsesVarExpectedTypeMetadata()
    {
        var diagnostics = await AnalyzeAllowingCompilationErrors(
            CommandSource("""
                static class Reader
                {
                    public static bool Match(Command command) => command is
                        { kind: Command.Kind.Move, move: { speed: string speed } };
                }
                """),
            new TypeSafetyAnalyzer());

        var diagnostic = AssertSingle(diagnostics, "SPIRE011");
        Assert.Equal("var", diagnostic.Properties["ExpectedType"]);
    }

    private static Microsoft.CodeAnalysis.Diagnostic AssertSingle(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string id)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        return diagnostic;
    }

    private static string CommandSource(string consumer)
        => """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
            }
            """ + consumer;

    private static string NestedOrdinaryStructSource(string consumer)
        => """
            using Houtamelo.Spire;
            public struct Foo
            {
                public string value;
            }

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(Foo foo, int value);
                [Variant] public static partial Command Stop();
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
        params DiagnosticAnalyzer[] analyzers)
        => await AnalyzeCore(source, allowCompilationErrors: false, analyzers);

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeAllowingCompilationErrors(
        string source,
        params DiagnosticAnalyzer[] analyzers)
        => await AnalyzeCore(source, allowCompilationErrors: true, analyzers);

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeCore(
        string source,
        bool allowCompilationErrors,
        params DiagnosticAnalyzer[] analyzers)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "variant-payload-consumer.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        if (!allowCompilationErrors)
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var diagnostics = await outputCompilation
            .WithAnalyzers(analyzers.ToImmutableArray())
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics
            .Where(diagnostic => diagnostic.Id is
                "SPIRE009" or "SPIRE011" or "SPIRE012" or "SPIRE013" or "SPIRE014")
            .ToImmutableArray();
    }
}
