using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class GuardBooleanFlowTests
{
    [Fact]
    public Task Pass_KindEqualsBothOperandOrders()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Direct(Command command)
                    => command.kind == Command.Kind.Move ? command.move.speed : 0f;

                public static float Reversed(Command command)
                    => Command.Kind.Move == command.kind ? command.move.speed : 0f;
            }
            """));

    [Fact]
    public Task Pass_KindNotEqualsEarlyReturnBothOrders()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Direct(Command command)
                {
                    if (command.kind != Command.Kind.Stop)
                        return 0f;
                    return command.stop.GetHashCode();
                }

                public static float Reversed(Command command)
                {
                    if (Command.Kind.Stop != command.kind)
                        return 0f;
                    return command.stop.GetHashCode();
                }
            }
            """));

    [Fact]
    public Task Pass_NegatedKindEquality()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!(command.kind == Command.Kind.Move))
                        return 0f;
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_IsVariantPositiveAndReversedBooleanComparison()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float IsPredicate(Command command)
                    => command.IsMove ? command.move.speed : 0f;

                public static float EqualsTrue(Command command)
                    => command.IsMove == true ? command.move.speed : 0f;

                public static float ReversedEqualsTrue(Command command)
                    => true == command.IsMove ? command.move.speed : 0f;

                public static float DoubleNegation(Command command)
                    => !!command.IsMove ? command.move.speed : 0f;
            }
            """));

    [Fact]
    public Task Pass_IsVariantNegativeComparisonEarlyExit()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float EqualsFalse(Command command)
                {
                    if (command.IsMove == false)
                        return 0f;
                    return command.move.speed;
                }

                public static float ReversedEqualsFalse(Command command)
                {
                    if (false == command.IsMove)
                        return 0f;
                    return command.move.speed;
                }

                public static float NotEqualsTrue(Command command)
                {
                    if (command.IsMove != true)
                        return 0f;
                    return command.move.speed;
                }

                public static float Negated(Command command)
                {
                    if (!command.IsMove)
                        return 0f;
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_CompoundBooleanComparisonKeepsTrueFacts()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Direct(Command command, bool ready)
                {
                    if ((command.IsMove && ready) == true)
                        return command.move.speed;
                    return 0f;
                }

                public static float Reversed(Command command, bool ready)
                {
                    if (true == (command.IsMove && ready))
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_NegatedCompoundBooleanComparison()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (!((command.IsMove && ready) == false))
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_SavedPredicateBooleanComparisons()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float EqualsTrue(Command command)
                {
                    var move = command.IsMove;
                    if (move == true)
                        return command.move.speed;
                    return 0f;
                }

                public static float ReversedEqualsTrue(Command command)
                {
                    var move = command.IsMove;
                    if (true == move)
                        return command.move.speed;
                    return 0f;
                }

                public static float NotEqualsFalse(Command command)
                {
                    var move = command.IsMove;
                    if (move != false)
                        return command.move.speed;
                    return 0f;
                }

                public static float EqualsFalseEarlyExit(Command command)
                {
                    var move = command.IsMove;
                    if (move == false)
                        return 0f;
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_CompoundComparisonTracksMultipleReceivers()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Direct(Command first, Command second)
                {
                    if ((first.IsMove && second.IsMove) == true)
                        return first.move.speed + second.move.speed;
                    return 0f;
                }

                public static float Reversed(Command first, Command second)
                {
                    if (true == (first.IsMove && second.IsMove))
                        return first.move.speed + second.move.speed;
                    return 0f;
                }

                public static float Negated(Command first, Command second)
                {
                    if (!((first.IsMove && second.IsMove) == false))
                        return first.move.speed + second.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OverwritingSavedPredicateInvalidatesCorrelation()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                private static void Set(ref bool value)
                    => value = true;

                public static float Direct(Command command)
                {
                    var move = command.IsMove;
                    move = true;
                    if (move)
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }

                public static float ViaRef(Command command)
                {
                    var move = command.IsMove;
                    Set(ref move);
                    if (move)
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }

                public static float FreshRead(Command command)
                {
                    var move = command.IsMove;
                    move = true;
                    move = command.IsMove;
                    if (move)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_LoopReevaluatesCompoundGuardsAfterMutation()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready, bool unknownFlag)
                {
                    var total = 0f;
                    while (unknownFlag)
                    {
                        if ((command.IsMove && ready) == true)
                            total += command.move.speed;
                        command = Command.Stop();
                    }
                    return total;
                }
            }
            """));

    [Fact]
    public Task Error_LoopMutationDoesNotReviveCapturedPredicate()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready, bool unknownFlag)
                {
                    while (unknownFlag)
                    {
                        var saved = command.IsMove && ready;
                        command = Command.Stop();
                        if (saved)
                            return [|SPIRE014:command.move|].speed;
                    }
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_FreshDifferentGuardCannotReviveEarlierCompoundCheck()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                private static bool Replace(ref Command command)
                {
                    command = Command.Stop();
                    return true;
                }

                public static float Read(Command command)
                {
                    if ((command.IsMove && Replace(ref command) && command.IsStop) == true)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_BitwiseRightMutationInvalidatesEarlierOperand()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                private static bool Replace(ref Command command)
                {
                    command = Command.Stop();
                    return true;
                }

                public static float Read(Command command)
                {
                    if (command.IsMove & Replace(ref command))
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_AndShortCircuitRhsGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (ready && command.kind == Command.Kind.Move)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ShortCircuitPredicateRhsAccessor()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static bool Read(Command command)
                {
                    if (command.IsMove && command.move.speed > 0f)
                        return command.move.speed > 1f;
                    if (!command.IsMove || command.move.speed > 0f)
                        return true;
                    return false;
                }
            }
            """));

    [Fact]
    public Task Error_OrTrueBodyDoesNotInheritRhsProof()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!command.IsMove || command.move.speed > 0f)
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_AndKindThenUnrelatedCondition()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.kind == Command.Kind.Move && ready)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_OrEarlyExitComplement()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.kind != Command.Kind.Move || !ready)
                        return 0f;
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_OrRhsGuardAfterFalseLeft()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (ready || command.kind != Command.Kind.Move)
                        return 0f;
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_OrOfKindsThenComplement()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static int Read(Command command)
                {
                    if (command.kind == Command.Kind.Move || command.kind == Command.Kind.Stop)
                        return 0;
                    return command.idle.GetHashCode();
                }
            }
            """));

    [Fact]
    public Task Pass_ContradictoryAndIsUnreachable()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move && command.kind == Command.Kind.Stop)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_NestedPredicateComposition()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!(command.IsStop || command.IsIdle))
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_PropertyPatternTrueWithPayloadRestriction()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command is { IsMove: true, move: { speed: > 0f } })
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_DirectFactoryAccessor()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static int Read()
                    => Command.Stop().stop.GetHashCode();
            }
            """));

    [Fact]
    public Task Pass_NameofAccessorDoesNotEvaluateReceiver()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static string Read(Command command)
                    => nameof(command.move);
            }
            """));

    [Fact]
    public Task Pass_ExplicitGuardOnSingletonUnion()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.SingletonSource("""
            static class Reader
            {
                public static SingleCommandTypes.Variants.Only Read(SingleCommand value)
                {
                    if (value.kind == SingleCommand.Kind.Only)
                        return value.only;
                    throw new System.InvalidOperationException();
                }
            }
            """));

    [Fact]
    public Task Error_KindEqualsWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static CommandTypes.Variants.Stop Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        return [|SPIRE013:command.stop|];
                    return Command.Stop().stop;
                }
            }
            """));

    [Fact]
    public Task Error_KindNotEqualsTrueBranchWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind != Command.Kind.Move)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_NegatedKindEqualityWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!(command.kind == Command.Kind.Move))
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_NegatedIsVariantTrueBranchWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!command.IsMove)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_IsVariantFalseComparisonWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove == false)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_AndGuardWrongVariant()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.kind == Command.Kind.Move && ready)
                        return [|SPIRE013:command.stop|].GetHashCode();
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_CompoundComparisonAfterMutation()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                private static bool Replace(ref Command command)
                {
                    command = Command.Stop();
                    return true;
                }

                public static float Read(Command command)
                {
                    if ((command.IsMove && Replace(ref command)) == true)
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_BooleanBitwiseAndDoesNotGuardRhs()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static bool Read(Command command)
                    => command.kind == Command.Kind.Move & [|SPIRE014:command.move|].speed > 0f;
            }
            """));

    [Fact]
    public Task Error_BitwiseAndFalseEdgeIsNotLeftComplement()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.IsMove & ready)
                        return 0f;
                    return [|SPIRE014:command.stop|].GetHashCode();
                }
            }
            """));

    [Fact]
    public Task Error_WrappedAndFalseEdgeIsNotLeftComplement()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if ((command.IsMove && ready) == true)
                        return 0f;
                    return [|SPIRE014:command.stop|].GetHashCode();
                }
            }
            """));

    [Fact]
    public Task Pass_BitwiseAndTrueEdgeCombinesPureFacts()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.IsMove & ready)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_BooleanBitwiseOrDoesNotGuardRhs()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static bool Read(Command command)
                    => command.kind != Command.Kind.Move | [|SPIRE014:command.move|].speed > 0f;
            }
            """));

    [Fact]
    public Task Error_OrGuardPartialPossibleSet()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, bool ready)
                {
                    if (command.kind == Command.Kind.Move || ready)
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OrKindsPossibleSet()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move || command.kind == Command.Kind.Stop)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_AndExclusionsPossibleSet()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind != Command.Kind.Move && command.kind != Command.Kind.Stop)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_NestedPredicatePossibleSet()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove || command.IsStop)
                        return [|SPIRE013:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_PayloadRestrictedPatternDoesNotProveComplement()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command is { IsMove: true, move: { speed: > 0f } })
                        return 0f;
                    return [|SPIRE014:command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_PayloadPatternWithoutDiscriminator()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command is { [|SPIRE014:move|]: var payload })
                        return [|SPIRE014:command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_UnguardedSingletonUnionStillUnknown()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.SingletonSource("""
            static class Reader
            {
                public static SingleCommandTypes.Variants.Only Read(SingleCommand value)
                    => [|SPIRE014:value.only|];
            }
            """));

    [Fact]
    public Task Error_FactoryAssignedLocalNoImplicitGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read()
                {
                    var command = Command.Move(0, 0f);
                    return [|SPIRE014:command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_FactoryReturnedValueNoImplicitGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                private static Command GetCommand() => Command.Move(0, 0f);

                public static float Read()
                    => [|SPIRE014:GetCommand().move|].speed;
            }
            """));
}
