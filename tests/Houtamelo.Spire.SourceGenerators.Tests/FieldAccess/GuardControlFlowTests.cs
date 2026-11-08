using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class GuardControlFlowTests
{
    [Fact]
    public async Task Pass_IfElseComplement()
    {
        await GuardFlowTestHelper.CheckAsync(TwoVariantSource("""
            public static int Read(Command command)
            {
                if (command.kind == Command.Kind.Move)
                    return (int)command.move.speed;
                return command.stop.GetHashCode();
            }
            """));
    }

    [Fact]
    public async Task Pass_EarlyReturnAndThrowComplement()
    {
        await GuardFlowTestHelper.CheckAsync(TwoVariantSource("""
            public static int ReadAfterReturn(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;
                return (int)command.move.speed;
            }

            public static int ReadAfterThrow(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    throw new InvalidOperationException();
                return (int)command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Pass_ElseIfChainTracksEachBranch()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                if (command.kind == Command.Kind.Move)
                    return (int)command.move.speed;
                else if (command.kind == Command.Kind.Stop)
                    return command.stop.GetHashCode();
                else
                    return command.idle.GetHashCode();
            }
            """));
    }

    [Fact]
    public async Task Pass_TernaryTrueAndFalseKindGuards()
    {
        await GuardFlowTestHelper.CheckAsync(TwoVariantSource("""
            public static int ReadMove(Command command)
                => command.kind == Command.Kind.Move
                    ? (int)command.move.speed
                    : 0;

            public static int ReadStop(Command command)
                => command.kind != Command.Kind.Move
                    ? command.stop.GetHashCode()
                    : 0;
            """));
    }

    [Fact]
    public async Task Pass_SwitchStatementAndExpressionMatchingArms()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int ReadStatement(Command command)
            {
                switch (command.kind)
                {
                    case Command.Kind.Move:
                        return (int)command.move.speed;
                    case Command.Kind.Stop:
                        return command.stop.GetHashCode();
                    default:
                        return command.idle.GetHashCode();
                }
            }

            public static int ReadExpression(Command command)
                => command.kind switch
                {
                    Command.Kind.Move => (int)command.move.speed,
                    Command.Kind.Stop => command.stop.GetHashCode(),
                    _ => command.idle.GetHashCode(),
                };
            """));
    }

    [Fact]
    public async Task Pass_PropertyPatternKindGuard()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command is { kind: Command.Kind.Move, move: var payload })
                    return payload.speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Pass_PayloadPositionalPattern()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command is { IsMove: true, move: var (destination, speed) })
                    return destination + speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Pass_SwitchDefaultComplement()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                switch (command.kind)
                {
                    case Command.Kind.Move:
                        return (int)command.move.speed;
                    case Command.Kind.Stop:
                        return command.stop.GetHashCode();
                    default:
                        return command.idle.GetHashCode();
                }
            }
            """));
    }

    [Fact]
    public async Task Pass_WhenGuardMatchingAccessor()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
                => command.kind switch
                {
                    Command.Kind.Move when command.move.speed > 0 => command.move.speed,
                    _ => 0,
                };
            """));
    }

    [Fact]
    public async Task Pass_DetachedPayloadAfterPropertyPattern()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command is { IsMove: true, move: var payload })
                    return payload.speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Pass_WhileGuardBreakOrContinue()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                var result = 0;
                for (var index = 0; index < 2; index++)
                {
                    if (command.kind != Command.Kind.Move)
                        break;
                    result += (int)command.move.speed;
                }

                for (var index = 0; index < 2; index++)
                {
                    if (command.kind != Command.Kind.Move)
                        continue;
                    result += (int)command.move.speed;
                }

                return result;
            }
            """));
    }

    [Fact]
    public async Task Pass_ForLoopGuardEachIteration()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                var result = 0;
                for (var index = 0; index < 3; index++)
                {
                    if (command.kind != Command.Kind.Move)
                        continue;
                    result += (int)command.move.speed;
                }
                return result;
            }
            """));
    }

    [Fact]
    public async Task Pass_LambdaOwnGuard()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                Func<Command, int> reader = value =>
                {
                    if (value.kind != Command.Kind.Move)
                        return 0;
                    return (int)value.move.speed;
                };
                return reader(command);
            }
            """));
    }

    [Fact]
    public async Task Pass_LocalFunctionOwnGuard()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                static int ReadLocal(Command value)
                {
                    if (value.kind != Command.Kind.Move)
                        return 0;
                    return (int)value.move.speed;
                }

                return ReadLocal(command);
            }
            """));
    }

    [Fact]
    public async Task Pass_ExceptionHandlersOwnGuards()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                var result = 0;
                try
                {
                    throw new InvalidOperationException();
                }
                catch
                {
                    if (command.kind == Command.Kind.Stop)
                        result += command.stop.GetHashCode();
                }
                finally
                {
                    if (command.kind == Command.Kind.Idle)
                        result += command.idle.GetHashCode();
                }

                return result;
            }
            """));
    }

    [Fact]
    public async Task Pass_ByValueProofSurvivesUnchangedCatchAndFinally()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float ReadCatch(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                try
                {
                    throw new InvalidOperationException();
                }
                catch
                {
                    return command.move.speed;
                }
            }

            public static float ReadFinally(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                try
                {
                    return 0;
                }
                finally
                {
                    _ = command.move.speed;
                }
            }
            """));
    }

    [Fact]
    public async Task Pass_SingleEvaluationPropertyPattern()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantWithHolderSource("""
            public static float Read(Holder holder)
            {
                if (holder.Current is { IsMove: true, move: var payload })
                    return payload.speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_PartialExitDoesNotEstablishComplement()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command, bool flag)
            {
                if (command.kind != Command.Kind.Move)
                {
                    if (flag)
                        return 0;
                }

                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_SiblingBranchGuardDoesNotReach()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command, bool flag)
            {
                if (flag)
                {
                    if (command.kind == Command.Kind.Move)
                        return command.move.speed;
                }

                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_TernaryUnrelatedCondition()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command, bool ready)
                => ready ? [|SPIRE014:command.move|].speed : 0;
            """));
    }

    [Fact]
    public async Task Error_SwitchGroupedKindsWrongAccessor()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                switch (command.kind)
                {
                    case Command.Kind.Move:
                    case Command.Kind.Stop:
                        return [|SPIRE013:command.move|].speed;
                    default:
                        return 0;
                }
            }
            """));
    }

    [Fact]
    public async Task Error_SwitchDefaultWrongAccessor()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                switch (command.kind)
                {
                    case Command.Kind.Move:
                        return command.move.speed;
                    case Command.Kind.Stop:
                        return 0;
                    default:
                        return [|SPIRE013:command.move|].speed;
                }
            }
            """));
    }

    [Fact]
    public async Task Error_SwitchOrPatternWrongAccessor()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
                => command.kind switch
                {
                    Command.Kind.Move or Command.Kind.Stop => [|SPIRE013:command.move|].speed,
                    _ => 0,
                };
            """));
    }

    [Fact]
    public async Task Error_WhenGuardWrongAccessor()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
                => command.kind switch
                {
                    Command.Kind.Move when [|SPIRE013:command.stop|].GetHashCode() >= 0 => 1,
                    _ => 0,
                };
            """));
    }

    [Fact]
    public async Task Error_LoopBodyPartialGuard()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command, bool ready)
            {
                while (ready)
                    return [|SPIRE014:command.move|].speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_LoopBackedgeAssignment()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                for (var index = 0; index < 2; index++)
                {
                    if (command.kind != Command.Kind.Move)
                        continue;
                    if (index == 0)
                        command = Command.Stop();
                }

                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DoWhileFirstIterationUnknown()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                var result = 0f;
                do
                {
                    result += [|SPIRE014:command.move|].speed;
                }
                while (command.kind == Command.Kind.Move);
                return result;
            }
            """));
    }

    [Fact]
    public async Task Error_OuterGuardDoesNotEnterLambda()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                Func<float> reader = () => [|SPIRE014:command.move|].speed;
                return reader();
            }
            """));
    }

    [Fact]
    public async Task Error_OuterGuardDoesNotEnterLocalFunction()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                float ReadLocal() => [|SPIRE014:command.move|].speed;
                return ReadLocal();
            }
            """));
    }

    [Fact]
    public async Task Error_AwaitInvalidatesProof()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantWithHolderSource("""
            public static async Task<float> Read(Holder holder)
            {
                if (holder.command.kind != Command.Kind.Move)
                    return 0;
                await Task.Yield();
                return [|SPIRE014:holder.command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_YieldInvalidatesProof()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantWithHolderSource("""
            public static IEnumerable<float> Read(Holder holder)
            {
                if (holder.command.kind != Command.Kind.Move)
                    yield break;
                yield return 0;
                yield return [|SPIRE014:holder.command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_FinallyHasNoIncomingProof()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command, bool enterGuard)
            {
                try
                {
                    if (enterGuard && command.kind == Command.Kind.Move)
                        return 0;
                }
                finally
                {
                    _ = [|SPIRE014:command.move|].speed;
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_CatchHasNoIncomingProof()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static void MutateAndThrow(ref Command command)
            {
                command = Command.Stop();
                throw new InvalidOperationException();
            }

            public static float Read(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                try
                {
                    MutateAndThrow(ref command);
                }
                catch
                {
                    return [|SPIRE014:command.move|].speed;
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_FinallyWriteInvalidatesContinuation()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                try
                {
                    _ = command.move.speed;
                }
                finally
                {
                    command = Command.Stop();
                }

                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_SwitchSnapshotDoesNotGuardReassignedSource()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static bool Replace(ref Command command)
            {
                command = Command.Stop();
                return true;
            }

            public static float Read(ref Command command)
            {
                return command switch
                {
                    { IsMove: true } when Replace(ref command)
                        => [|SPIRE014:command.move|].speed,
                    _ => 0,
                };
            }
            """));
    }

    [Fact]
    public async Task Error_PropertyPatternGetterMutatesOriginalStorage()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantPropertyGetterSource("""
            public static float Read()
            {
                Reader.Shared = Command.Move(1, 2);
                if (Reader.Shared is { IsMove: true, Trigger: true })
                    return [|SPIRE014:Reader.Shared.move|].speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_ExceptionBeforeRefRebindKeepsOldBinding()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static void MayThrow(bool fail)
            {
                if (fail)
                    throw new InvalidOperationException();
            }

            public static float Read(Command command, Command proven, bool fail)
            {
                ref Command alias = ref command;
                if (!proven.IsMove)
                    return 0;

                try
                {
                    MayThrow(fail);
                    alias = ref proven;
                }
                catch
                {
                    return [|SPIRE014:alias.move|].speed;
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_FilteredCatchBodyIsAnalyzed()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command, bool flag)
            {
                try
                {
                    throw new InvalidOperationException();
                }
                catch (Exception) when (flag)
                {
                    return [|SPIRE014:command.move|].speed;
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Pass_FilterGuardEstablishesVariant()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static float Read(Command command)
            {
                try
                {
                    throw new InvalidOperationException();
                }
                catch (Exception) when (command.IsMove)
                {
                    return command.move.speed;
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_FilterGuardWrongVariant()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static int Read(Command command)
            {
                try
                {
                    throw new InvalidOperationException();
                }
                catch (Exception) when (command.IsMove)
                {
                    return [|SPIRE013:command.stop|].GetHashCode();
                }

                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_FalseFilterMutationReachesNextHandler()
    {
        await GuardFlowTestHelper.CheckAsync(ThreeVariantSource("""
            public static bool ReplaceThenFalse(ref Command command)
            {
                command = Command.Stop();
                return false;
            }

            public static float Read(Command command)
            {
                if (command.kind != Command.Kind.Move)
                    return 0;

                try
                {
                    throw new InvalidOperationException();
                }
                catch (Exception) when (ReplaceThenFalse(ref command))
                {
                    return 0;
                }
                catch
                {
                    return [|SPIRE014:command.move|].speed;
                }

                return 0;
            }
            """));
    }

    private static string TwoVariantSource(string body)
        => Source("""
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
            }
            """, body);

    private static string ThreeVariantSource(string body)
        => Source("""
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }
            """, body);

    private static string ThreeVariantWithHolderSource(string body)
        => Source("""
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }

            public sealed class Holder
            {
                public Command command;
                public Command Current => command;
            }
            """, body);

    private static string ThreeVariantPropertyGetterSource(string body)
        => Source("""
            [DiscriminatedUnion]
            public partial struct Command
            {
                public bool Trigger
                {
                    get
                    {
                        Reader.Shared = Command.Stop();
                        return true;
                    }
                }

                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }

            public static class Reader
            {
                public static Command Shared;
            }
            """, body);

    private static string Source(string declarations, string body)
        => """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Houtamelo.Spire;

            """ + declarations + """

            public static class Scenario
            {
            """ + body + """
            }
            """;
}
