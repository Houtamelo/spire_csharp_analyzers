using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class GuardReceiverStabilityTests
{
    [Fact]
    public Task Pass_SameParameterReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_SameLocalSnapshotReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var snapshot = command;
                    if (snapshot.kind == Command.Kind.Move)
                        return snapshot.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_IdentityPreservingConversion()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (((Command)command).kind == Command.Kind.Move)
                        return ((Command)command).move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ThisFieldPathReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Reader
            {
                private Command _command = Command.Stop();

                public float Read()
                {
                    if (this._command.kind == Command.Kind.Move)
                        return this._command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_NestedFieldPathReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public State State = new();
            }

            sealed class State
            {
                public Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.State.Command.kind == Command.Kind.Move)
                        return holder.State.Command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_StaticFieldPathReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Store
            {
                public static global::Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static float Read()
                {
                    if (Store.Command.kind == Command.Kind.Move)
                        return Store.Command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_GeneratedAccessorAndPayloadProjectionsStayPure()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.command.IsMove)
                        return holder.command.move.destination + holder.command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ByValueCallsPreserveStableLocal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Observe(Command value)
                {
                }

                private static void ObserveIn(in Command value)
                {
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        Observe(command);
                        ObserveIn(in command);
                        return command.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_CapturedSnapshotLocalStable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Observe(Command value)
                {
                }

                public static float Read(Command command)
                {
                    var snapshot = command;
                    if (snapshot.kind != Command.Kind.Move)
                        return 0f;

                    System.Action inspect = () =>
                    {
                        _ = snapshot.IsMove;
                    };

                    inspect();
                    Observe(snapshot);
                    return snapshot.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_LocalSnapshotSurvivesUnrelatedAwaitAndYield()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static async System.Threading.Tasks.Task<float> ReadAsync(Command command)
                {
                    var snapshot = command;
                    if (snapshot.kind != Command.Kind.Move)
                        return 0f;

                    await System.Threading.Tasks.Task.Yield();
                    return snapshot.move.speed;
                }

                public static System.Collections.Generic.IEnumerable<float> ReadIterator(Command command)
                {
                    var snapshot = command;
                    if (snapshot.kind != Command.Kind.Move)
                        yield break;

                    yield return 0f;
                    yield return snapshot.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_RefMutatingCallArgumentEvaluation()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Use(ref Command command, float speed)
                {
                    command = Command.Stop();
                }

                private static void Use(float speed, ref Command command)
                {
                    command = Command.Stop();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        Use(ref command, command.move.speed);
                    }

                    return 0f;
                }

                public static float ReadPayloadFirst(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        Use(command.move.speed, ref command);

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_FactoryAssignedThenExplicitGuard()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read()
                {
                    var command = Command.Move(0, 1f);
                    if (command.kind == Command.Kind.Move)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ReadonlyInParameterSameReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(in Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_VolatileFieldSnapshotLocalStable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            sealed class Root
            {
                public volatile Holder Current = new();
            }

            static class Store
            {
                public static readonly Root Root = new();
            }

            static class Reader
            {
                public static float Read()
                {
                    var snapshot = Store.Root.Current.Command;
                    if (snapshot.kind == Command.Kind.Move)
                        return snapshot.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_DirectRefAliasOwnGuard()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    ref Command alias = ref command;
                    if (alias.kind == Command.Kind.Move)
                        return alias.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_RecheckAfterMutationRestoresProof()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command, Command replacement)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        command = replacement;
                        if (command.kind == Command.Kind.Move)
                            return command.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnrelatedFieldWritePreservesProof()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command command = Command.Stop();
                public int Count;
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.command.kind == Command.Kind.Move)
                    {
                        holder.Count++;
                        return holder.command.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_PayloadBeforeNestedMutationArgument()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static int Mutate(ref Command command)
                {
                    command = Command.Stop();
                    return 0;
                }

                private static void Consume(float speed, int ignored)
                {
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        Consume(command.move.speed, Mutate(ref command));

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_DeconstructionAssignmentInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command, Command replacement)
                {
                    var ignored = 0;
                    if (command.kind == Command.Kind.Move)
                    {
                        (command, ignored) = (replacement, 0);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_SetterEvaluatesValueBeforeMutation()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();

                public float Sink
                {
                    set => this.Command = global::Command.Stop();
                }
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Command.IsMove)
                        holder.Sink = holder.Command.move.speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_SetterMutationInvalidatesLaterAccess()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();

                public float Sink
                {
                    set => this.Command = global::Command.Stop();
                }
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Command.IsMove)
                    {
                        holder.Sink = holder.Command.move.speed;
                        return [|SPIRE014:holder.Command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_DifferentParameterReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command first, Command second)
                {
                    if (first.kind == Command.Kind.Move)
                        return [|SPIRE014:second.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_LocalAssignmentInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command, Command replacement)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        command = replacement;
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_BoxingConversionBreaksReceiverIdentity()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind != Command.Kind.Move)
                        return 0f;

                    object boxed = command;
                    var unboxed = (Command)boxed;
                    return [|SPIRE014:unboxed.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_DifferentFieldPathReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Branch
            {
                public Command Command = global::Command.Stop();
            }

            sealed class Holder
            {
                public Branch Left = new();
                public Branch Right = new();
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Left.Command.kind == Command.Kind.Move)
                        return [|SPIRE014:holder.Right.Command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_FieldAndAncestorWritesInvalidatePath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class State
            {
                public Command Command = global::Command.Stop();
            }

            sealed class Holder
            {
                public State State = new();
            }

            static class Reader
            {
                public static float Read(Holder holder, Command replacement)
                {
                    if (holder.State.Command.kind == Command.Kind.Move)
                    {
                        holder.State.Command = replacement;
                        var leaf = [|SPIRE014:holder.State.Command.move|].speed;
                        holder.State = new State { Command = replacement };
                        return [|SPIRE014:holder.State.Command.move|].speed + leaf;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_StaticFieldWriteInvalidatesPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Store
            {
                public static global::Command Command = global::Command.Stop();
            }

            static class Reader
            {
                private static void Replace()
                {
                    Store.Command = Command.Stop();
                }

                public static float Read()
                {
                    if (Store.Command.kind != Command.Kind.Move)
                        return 0f;

                    Replace();
                    return [|SPIRE014:Store.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_RefArgumentInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Replace(ref Command command)
                {
                    command = Command.Stop();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        Replace(ref command);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OutArgumentInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Replace(out Command command)
                {
                    command = Command.Stop();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        Replace(out command);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OpaqueRefAliasInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static ref Command Alias(ref Command command)
                    => ref command;

                private static void Replace(ref Command command)
                {
                    command = Command.Stop();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        ref Command alias = ref Alias(ref command);
                        Replace(ref alias);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_MutationCapableStructCallInvalidatesPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            public partial struct Command
            {
                public void Reset()
                {
                    this = Stop();
                }
            }

            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        command.Reset();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_CapturedWriteOrClosureInvocationInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        System.Action mutate = () => command = Command.Stop();
                        mutate();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_AccessAfterRefMutatingCall()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Use(ref Command command, float speed)
                {
                    command = Command.Stop();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        Use(ref command, command.move.speed);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_AwaitOrYieldInvalidatesFieldPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static async System.Threading.Tasks.Task<float> ReadAsync(Holder holder)
                {
                    if (holder.Command.kind != Command.Kind.Move)
                        return 0f;

                    await System.Threading.Tasks.Task.Yield();
                    return [|SPIRE014:holder.Command.move|].speed;
                }

                public static System.Collections.Generic.IEnumerable<float> ReadIterator(Holder holder)
                {
                    if (holder.Command.kind != Command.Kind.Move)
                        yield break;

                    yield return 0f;
                    yield return [|SPIRE014:holder.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_PropertyGetterReceiverIsUnstable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                private Command _command = Command.Stop();
                public Command Current => _command;
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Current.kind == Command.Kind.Move)
                        return [|SPIRE014:holder.Current.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_IndexerReceiverIsUnstable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Commands
            {
                private readonly Command[] _values = { Command.Stop() };
                public Command this[int index] => _values[index];
            }

            sealed class Holder
            {
                public Commands Commands { get; } = new();
            }

            static class Reader
            {
                public static float Read(Holder holder, int index)
                {
                    if (holder.Commands[index].kind == Command.Kind.Move)
                        return [|SPIRE014:holder.Commands[index].move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_MethodCallReceiverIsUnstable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static Command GetCommand()
                    => Command.Move(0, 1f);

                public static float Read()
                {
                    if (GetCommand().kind == Command.Kind.Move)
                        return [|SPIRE014:GetCommand().move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_ConditionalAccessReceiverIsUnstable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                private Command _command = Command.Stop();
                public Command Current => _command;
            }

            static class Reader
            {
                public static float Read(Holder? holder)
                {
                    if (holder?.Current.IsMove == true)
                        return [|SPIRE014:holder.Current.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_UserDefinedConversionSideEffectBreaksIdentity()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            readonly struct CommandBox
            {
                private readonly Command _value;

                public CommandBox(Command value)
                {
                    _value = value;
                }

                public static explicit operator Command(CommandBox value)
                    => value._value;
            }

            static class Reader
            {
                public static float Read(Command command)
                {
                    var box = new CommandBox(command);
                    if (command.kind == Command.Kind.Move)
                        return [|SPIRE014:((Command)box).move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_VolatileFieldPathIsUnstable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            sealed class Root
            {
                public volatile Holder Current = new();
            }

            static class Store
            {
                public static readonly Root Root = new();
            }

            static class Reader
            {
                public static float Read()
                {
                    if (Store.Root.Current.Command.kind == Command.Kind.Move)
                        return [|SPIRE014:Store.Root.Current.Command.move|].speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_CapturedMutableLocalWriteInvalidates()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Invoke(System.Action action)
                {
                    action();
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        System.Action mutate = () => command = Command.Stop();
                        Invoke(mutate);
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_SharedFieldWriteThroughHelper()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            static class Reader
            {
                private static void Replace(Holder holder)
                {
                    holder.Command = Command.Stop();
                }

                public static float Read(Holder holder)
                {
                    if (holder.Command.kind != Command.Kind.Move)
                        return 0f;

                    Replace(holder);
                    return [|SPIRE014:holder.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_RefAliasToFieldInvalidatesPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Command.kind != Command.Kind.Move)
                        return 0f;

                    ref Command alias = ref holder.Command;
                    alias = Command.Stop();
                    return [|SPIRE014:holder.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_AncestorReplacementInvalidatesNestedPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Inner
            {
                public Command Command = global::Command.Stop();
            }

            sealed class Holder
            {
                public Inner Inner = new();
            }

            static class Reader
            {
                public static float Read(Holder holder)
                {
                    if (holder.Inner.Command.kind != Command.Kind.Move)
                        return 0f;

                    holder.Inner = new Inner();
                    return [|SPIRE014:holder.Inner.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_ShortCircuitInterveningMutation()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static bool Replace(ref Command command)
                {
                    command = Command.Stop();
                    return true;
                }

                public static float Read(Command command)
                {
                    if (command.IsMove && Replace(ref command) &&
                        [|SPIRE014:command.move|].speed > 0f)
                        return 1f;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_DirectRefAliasWriteInvalidatesOriginal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                    {
                        ref Command alias = ref command;
                        alias = Command.Stop();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_HeapAliasWriteInvalidatesOtherPath()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            sealed class Holder
            {
                public Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static float Read(Holder first, Holder second)
                {
                    if (first.Command.kind != Command.Kind.Move)
                        return 0f;

                    second.Command = Command.Stop();
                    return [|SPIRE014:first.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_NestedMutationArgumentBeforePayload()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static int Mutate(ref Command command)
                {
                    command = Command.Stop();
                    return 0;
                }

                private static void Consume(int ignored, float speed)
                {
                }

                public static float Read(Command command)
                {
                    if (command.kind == Command.Kind.Move)
                        Consume(Mutate(ref command), [|SPIRE014:command.move|].speed);

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_FinallyRefRebindJoinsAlternatives()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command proven, Command command, bool flag)
                {
                    if (proven.kind != Command.Kind.Move)
                        return 0f;

                    ref Command alias = ref proven;
                    try
                    {
                    }
                    finally
                    {
                        if (flag)
                            alias = ref command;
                        else
                            alias = ref proven;
                    }

                    return [|SPIRE014:alias.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_AmbiguousRefJoinDirectWriteInvalidatesOriginal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command first, Command second, bool flag)
                {
                    ref Command alias = ref first;
                    if (flag)
                        alias = ref second;

                    if (!first.IsMove)
                        return 0f;

                    alias = Command.Stop();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_AmbiguousRefJoinOpaqueCallInvalidatesOriginal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static void Replace(ref Command command)
                {
                    command = Command.Stop();
                }

                public static float Read(Command first, Command second, bool flag)
                {
                    ref Command alias = ref first;
                    if (flag)
                        alias = ref second;

                    if (!first.IsMove)
                        return 0f;

                    Replace(ref alias);
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_AmbiguousRefJoinMutatingInstanceInvalidatesOriginal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            public partial struct Command
            {
                public void Reset()
                {
                    this = Stop();
                }
            }

            static class Reader
            {
                public static float Read(Command first, Command second, bool flag)
                {
                    ref Command alias = ref first;
                    if (flag)
                        alias = ref second;

                    if (!first.IsMove)
                        return 0f;

                    alias.Reset();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_RefParametersMayAlias()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(ref Command first, ref Command second)
                {
                    if (!first.IsMove)
                        return 0f;

                    second = Command.Stop();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_InParameterCallbackMayMutateCallerStorage()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(in Command first, System.Action mutate)
                {
                    if (!first.IsMove)
                        return 0f;

                    mutate();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_MutatingStructGetterInvalidatesReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            public partial struct Command
            {
                public bool Mutate
                {
                    get
                    {
                        this = Stop();
                        return true;
                    }
                }
            }

            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove)
                    {
                        _ = command.Mutate;
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ReadonlyStructGetterPreservesUnescapedValue()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            public partial struct Command
            {
                public readonly bool Observe => true;
            }

            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove)
                    {
                        _ = command.Observe;
                        return command.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_AmbiguousRefSelectionWriteInvalidatesGuardedAlias()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command first, Command second, bool chooseFirst)
                {
                    ref Command alias = ref (chooseFirst ? ref first : ref second);
                    if (!alias.IsMove)
                        return 0f;

                    first = Command.Stop();
                    return [|SPIRE014:alias.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_OpaqueRefReturnAliasWriteInvalidatesOriginal()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Opaque
            {
                public static ref Command Select(ref Command command)
                    => ref command;
            }

            static class Reader
            {
                public static float Read(Command first)
                {
                    ref Command alias = ref Opaque.Select(ref first);
                    if (!first.IsMove)
                        return 0f;

                    alias = Command.Stop();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_RefParameterAndStaticFieldMayAlias()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Store
            {
                public static Command Command = global::Command.Stop();
            }

            static class Reader
            {
                public static float ReadField(ref Command external)
                {
                    if (!Store.Command.IsMove)
                        return 0f;

                    external = Command.Stop();
                    return [|SPIRE014:Store.Command.move|].speed;
                }

                public static float ReadRef(ref Command external)
                {
                    if (!external.IsMove)
                        return 0f;

                    Store.Command = Command.Stop();
                    return [|SPIRE014:external.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Pass_AmbiguousRefSelectionOwnGuardRemainsUsable()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static float Read(Command first, Command second, bool chooseFirst)
                {
                    ref Command alias = ref (chooseFirst ? ref first : ref second);
                    if (alias.IsMove)
                        return alias.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnescapedValueSurvivesUnrelatedRefTargetWrite()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Opaque
            {
                public static ref Command Select(ref Command command)
                    => ref command;
            }

            static class Reader
            {
                public static float Read(Command command, Command external)
                {
                    var snapshot = command;
                    ref Command alias = ref Opaque.Select(ref external);
                    if (!snapshot.IsMove)
                        return 0f;

                    alias = Command.Stop();
                    return snapshot.move.speed;
                }
            }
            """));

    [Fact]
    public Task Error_AddressExposedLocalInvalidatedByPointerWrite()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static unsafe float Read(Command command)
                {
                    Command* pointer = &command;
                    if (!command.IsMove)
                        return 0f;

                    *pointer = Command.Stop();
                    return [|SPIRE014:command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Pass_AddressTakenLocalWithoutPostGuardMutationRemainsProven()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static unsafe float Read(Command command)
                {
                    Command* pointer = &command;
                    if (!command.IsMove)
                        return 0f;

                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Error_IndirectWriteDoesNotForgetRefAliasBinding()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static unsafe float Read(Command first, Command external)
                {
                    ref Command alias = ref first;
                    Command* pointer = &external;
                    *pointer = Command.Stop();

                    if (!first.IsMove)
                        return 0f;

                    alias = Command.Stop();
                    return [|SPIRE014:first.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_PointerValueCallInvalidatesAddressExposedReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                private static unsafe void Replace(Command* pointer)
                {
                    *pointer = Command.Stop();
                }

                public static unsafe float Read(Command command)
                {
                    Command* pointer = &command;
                    if (!command.IsMove)
                        return 0f;

                    Replace(pointer);
                    return [|SPIRE014:command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_PointerElementWriteInvalidatesAddressExposedReceiver()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            static class Reader
            {
                public static unsafe float Read(Command command)
                {
                    Command* pointer = &command;
                    if (!command.IsMove)
                        return 0f;

                    pointer[0] = Command.Stop();
                    return [|SPIRE014:command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Pass_BuiltinReferenceConversionsPreserveReceiverField()
        => GuardFlowTestHelper.CheckAsync(CommandSource("""
            class BaseHolder
            {
                public Command Command = global::Command.Stop();
            }

            sealed class DerivedHolder : BaseHolder
            {
            }

            static class Reader
            {
                public static float Read(DerivedHolder holder)
                {
                    if (((BaseHolder)holder).Command.IsMove)
                        return ((BaseHolder)holder).Command.move.speed;
                    return 0f;
                }
            }
            """));

    private static string CommandSource(string consumer)
        => """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
            }
            """ + consumer;
}
