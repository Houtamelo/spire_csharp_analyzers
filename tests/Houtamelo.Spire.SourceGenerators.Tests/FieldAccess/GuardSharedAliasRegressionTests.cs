using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

/// Regression coverage for aliases that cross shared storage and ref-return writes.
public sealed class GuardSharedAliasRegressionTests
{
    [Fact]
    public Task Error_SharedFieldWriteInvalidatesDefiniteAliasGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Holder holder)
                {
                    ref Command alias = ref holder.Command;
                    if (alias.IsMove)
                    {
                        Replace(holder);
                        return [|SPIRE014:alias.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_SharedFieldWriteInvalidatesSavedAliasPredicate()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Holder holder)
                {
                    ref Command alias = ref holder.Command;
                    var move = alias.IsMove;
                    Replace(holder);
                    if (move)
                        return [|SPIRE014:alias.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_SharedWriteInvalidatesAmbiguousAliasTarget()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Holder holder, Command local, bool chooseHolder)
                {
                    ref Command alias = ref (chooseHolder ? ref holder.Command : ref local);
                    if (alias.IsMove)
                    {
                        Replace(holder);
                        return [|SPIRE014:alias.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnexposedValueAliasSurvivesUnrelatedSharedWrite()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Command command, Holder holder)
                {
                    ref Command alias = ref command;
                    if (alias.IsMove)
                    {
                        Replace(holder);
                        return alias.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_RefReturnTupleWriteInvalidatesRootGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var values = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref command, 1);
                    if (command.IsMove)
                    {
                        (values[0], _) = (Command.Stop(), 0);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_RefReturnTupleWritePreservesUnrelatedValueSnapshot()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var snapshot = command;
                    var values = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref command, 1);
                    if (snapshot.IsMove)
                    {
                        (values[0], _) = (Command.Stop(), 0);
                        return snapshot.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_LocalReferenceRebindDoesNotRetargetExistingAlias()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder, Holder replacement)
                {
                    ref Command alias = ref holder.Command;
                    holder = replacement;
                    if (holder.Command.IsMove)
                        return [|SPIRE014:alias.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_NestedReferenceRebindDoesNotRetargetExistingAlias()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Child Child = new();
            }

            sealed class Child
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder, Holder replacement)
                {
                    ref Command alias = ref holder.Child.Command;
                    holder.Child = replacement.Child;
                    if (holder.Child.Command.IsMove)
                        return [|SPIRE014:alias.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_FreshGuardRetainsAliasForSameFieldStorage()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                    => holder.Command = Command.Move(0, 5f);

                public static float Read(Holder holder)
                {
                    ref Command alias = ref holder.Command;
                    Replace(holder);
                    if (holder.Command.IsMove)
                        return alias.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_SameLocalValueReplacementRetainsRefAliasIdentity()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command initial)
                {
                    var command = initial;
                    ref var alias = ref command;
                    command = Command.Move(0, 17f);
                    if (command.IsMove)
                        return alias.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OpaqueNestedReferenceReplacementDetachesExistingAlias()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Child Child = new();
            }

            sealed class Child
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void ReplaceChild(Holder holder, Holder replacement)
                    => holder.Child = replacement.Child;

                public static float Read(Holder holder, Holder replacement)
                {
                    ref Command alias = ref holder.Child.Command;
                    ReplaceChild(holder, replacement);
                    if (holder.Child.Command.IsMove)
                        return [|SPIRE014:alias.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_MixedAliasTargetCanStillWriteRootAfterPrefixDetach()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Child Child = new();
            }

            sealed class Child
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder, Holder replacement, Command command, bool chooseHeap)
                {
                    ref Command alias = ref (chooseHeap ? ref holder.Child.Command : ref command);
                    holder.Child = replacement.Child;
                    if (command.IsMove)
                    {
                        alias = Command.Stop();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ReboundAliasClearsDetachedMayTarget()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Child Child = new();
            }

            sealed class Child
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Touch(Holder holder)
                    => holder.Child = new Child();

                public static float Read(
                    Holder holder,
                    Holder replacement,
                    Command command,
                    Command otherValueLocal,
                    bool chooseHeap)
                {
                    ref Command alias = ref (chooseHeap ? ref holder.Child.Command : ref command);
                    holder.Child = replacement.Child;
                    alias = ref otherValueLocal;
                    if (!command.IsMove)
                        return 0f;

                    Touch(holder);
                    alias = Command.Stop();
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Error_InvokedCallbackCapabilityStillInvalidatesLocalAndSharedTargets()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Opaque(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Command command, Holder holder)
                {
                    global::System.Action mutate = () =>
                    {
                        command = Command.Stop();
                        holder.Command = Command.Stop();
                    };

                    Opaque(holder);
                    if (command.IsMove)
                    {
                        mutate();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnusedAndHeapOnlyCallbacksDoNotInvalidateLocalTarget()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Holder
            {
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                private static void Opaque(Holder holder)
                    => holder.Command = Command.Stop();

                public static float Read(Command command, Holder holder)
                {
                    global::System.Action unused = () => command = Command.Stop();
                    global::System.Action mutate = () => holder.Command = Command.Stop();

                    Opaque(holder);
                    if (command.IsMove)
                    {
                        mutate();
                        return command.move.speed;
                    }

                    return 0f;
                }
            }
            """));
}
