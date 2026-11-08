using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class GuardEffectSafetyTests
{
    [Fact]
    public async Task Error_LocalFunctionMutatesCapturedUnion()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                void Mutate() => command = Command.Stop();
                Mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DelegateMutatingInstanceInvalidatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public void Reset() => this = Stop();
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                global::System.Action mutate = () => command.Reset();
                mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_UnknownDelegateReassignmentClearsOldEffects()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read()
            {
                var first = Command.Stop();
                var second = Command.Move(1, 2);
                global::System.Action write = () => first = Command.Stop();

                global::System.Action GetSecondAction()
                    => () => second = Command.Stop();

                write = GetSecondAction();
                if (!second.IsMove)
                    return 0;
                write();
                return [|SPIRE014:second.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DelegateJoinUnionsPossibleWrites()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(bool writeSecond)
            {
                var first = Command.Move(1, 2);
                var second = Command.Move(3, 4);
                global::System.Action write = () => first = Command.Stop();
                if (writeSecond)
                    write = () => second = Command.Stop();

                if (!first.IsMove || !second.IsMove)
                    return 0;
                write();
                return [|SPIRE014:first.move|].speed + [|SPIRE014:second.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_EscapedCallbackCanRunInOpaqueCall()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class CallbackStore
            {
                private global::System.Action? _callback;

                public void Register(global::System.Action callback)
                    => _callback = callback;

                public void Run()
                    => _callback?.Invoke();
            }
            """, """
            public static float Read(Command command)
            {
                var store = new CallbackStore();
                store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                store.Run();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ConstructorRefMutationInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public struct RefMutator
            {
                public RefMutator(ref Command command, float value)
                {
                    command = Command.Stop();
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                _ = new RefMutator(ref command, command.move.speed);
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ConstructorCallbackMayMutateCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public struct CallbackConstructor
            {
                public CallbackConstructor(global::System.Action callback)
                {
                    callback();
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                _ = new CallbackConstructor(() => command = Command.Stop());
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DynamicRefInvocationInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class DynamicMutator
            {
                public void Replace(ref Command command)
                    => command = Command.Stop();
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                dynamic mutator = new DynamicMutator();
                mutator.Replace(ref command);
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_UserOperatorsInvalidateSharedStorage()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public static Command operator +(Command left, Command right)
                {
                    Reader.Shared = Stop();
                    return left;
                }

                public static Command operator -(Command value)
                {
                    Reader.Shared = Stop();
                    return value;
                }
            }

            public static class Reader
            {
                public static Command Shared;
            }
            """, """
            public static float Read()
            {
                Reader.Shared = Command.Move(1, 2);
                if (!Reader.Shared.IsMove)
                    return 0;
                _ = Reader.Shared + Command.Stop();
                var first = [|SPIRE014:Reader.Shared.move|].speed;

                Reader.Shared = Command.Move(3, 4);
                if (!Reader.Shared.IsMove)
                    return 0;
                _ = -Reader.Shared;
                return first + [|SPIRE014:Reader.Shared.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_CustomEventAccessorsMutateValueReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public event global::System.Action Changed
                {
                    add => this = Stop();
                    remove => this = Stop();
                }
            }
            """, """
            public static float ReadAdd(Command command)
            {
                if (!command.IsMove)
                    return 0;
                command.Changed += static () => { };
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadRemove(Command command)
            {
                if (!command.IsMove)
                    return 0;
                command.Changed -= static () => { };
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_StructSetterMutationInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public int Replacement
                {
                    set => this = Stop();
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;
                command.Replacement = command.move.destination;
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_CapturedDeconstructionWriteInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                global::System.Action mutate = () =>
                    (command, _) = (Command.Stop(), 0);
                mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DynamicMemberAccessInvalidatesSharedStorage()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class DynamicReader
            {
                public object Value
                {
                    get
                    {
                        Reader.Shared = Command.Stop();
                        return new object();
                    }
                }
            }

            public static class Reader
            {
                public static Command Shared;
            }
            """, """
            public static float Read()
            {
                Reader.Shared = Command.Move(1, 2);
                if (!Reader.Shared.IsMove)
                    return 0;
                dynamic reader = new DynamicReader();
                _ = reader.Value;
                return [|SPIRE014:Reader.Shared.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Pass_UnknownCallbackCannotReachUnescapedValue()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command, global::System.Action external)
            {
                var callback = external;
                if (!command.IsMove)
                    return 0;
                callback();
                return command.move.speed;
            }

            public static float ReadReassigned(Command command, global::System.Action external)
            {
                global::System.Action callback = static () => { };
                callback = external;
                if (!command.IsMove)
                    return 0;
                callback();
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_FunctionPointerRefCallInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static bool Replace(ref Command command, float value)
            {
                command = Command.Stop();
                return true;
            }

            public static unsafe float Read(Command command)
            {
                delegate*<ref Command, float, bool> mutate = &Replace;
                if (!command.IsMove)
                    return 0;

                _ = mutate(ref command, command.move.speed);
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_SetterCallbackCanRunImmediately()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class CallbackHolder
            {
                private global::System.Action? _callback;

                public global::System.Action? Callback
                {
                    get => _callback;
                    set
                    {
                        _callback = value;
                        value?.Invoke();
                    }
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                var holder = new CallbackHolder();
                holder.Callback = () => command = Command.Stop();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_EventCallbackCanRunImmediately()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public event global::System.Action Changed
                {
                    add => value();
                    remove { }
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0;

                command.Changed += () => command = Command.Stop();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_StoredCallbackGetterCanMutateCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class CallbackGetterStore
            {
                private global::System.Action? _callback;

                public global::System.Action? Callback
                {
                    set => _callback = value;
                }

                public int Trigger
                {
                    get
                    {
                        _callback?.Invoke();
                        return 0;
                    }
                }
            }
            """, """
            public static float Read(Command command)
            {
                var store = new CallbackGetterStore();
                store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;

                _ = store.Trigger;
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_StoredCallbackSetterCanMutateCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class CallbackSetterStore
            {
                private global::System.Action? _callback;

                public global::System.Action? Callback
                {
                    set => _callback = value;
                }

                public int Trigger
                {
                    set => _callback?.Invoke();
                }
            }
            """, """
            public static float Read(Command command)
            {
                var store = new CallbackSetterStore();
                store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;

                store.Trigger = 1;
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_StoredCallbackEventCanMutateCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class CallbackEventStore
            {
                private global::System.Action? _callback;

                public global::System.Action? Callback
                {
                    set => _callback = value;
                }

                public event global::System.Action Triggered
                {
                    add => _callback?.Invoke();
                    remove { }
                }
            }
            """, """
            public static float Read(Command command)
            {
                var store = new CallbackEventStore();
                store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;

                store.Triggered += static () => { };
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ClosureConstructorRefMutationInvalidatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public struct RefMutator
            {
                public RefMutator(ref Command command)
                {
                    command = Command.Stop();
                }
            }
            """, """
            public static float Read(Command command)
            {
                global::System.Action mutate = () => _ = new RefMutator(ref command);
                if (!command.IsMove)
                    return 0;

                mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ClosureDynamicRefMutationInvalidatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class DynamicMutator
            {
                public void Replace(ref Command command)
                    => command = Command.Stop();
            }
            """, """
            public static float Read(Command command)
            {
                global::System.Action mutate = () =>
                {
                    dynamic mutator = new DynamicMutator();
                    mutator.Replace(ref command);
                };
                if (!command.IsMove)
                    return 0;

                mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ClosureFunctionPointerRefMutationInvalidatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class FunctionPointerMutator
            {
                public static bool Replace(ref Command command)
                {
                    command = Command.Stop();
                    return true;
                }
            }
            """, """
            public static unsafe float Read(Command command)
            {
                global::System.Action mutate = () =>
                {
                    delegate*<ref Command, bool> pointer = &FunctionPointerMutator.Replace;
                    _ = pointer(ref command);
                };
                if (!command.IsMove)
                    return 0;

                mutate();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_LocalFunctionWrapperMutatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command)
            {
                void Mutate() => command = Command.Stop();
                void Wrapper() => Mutate();

                if (!command.IsMove)
                    return 0;
                Wrapper();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadRecursive(Command command, bool recurse)
            {
                void First()
                {
                    if (recurse)
                        Second();
                    else
                        command = Command.Stop();
                }

                void Second() => First();

                if (!command.IsMove)
                    return 0;
                First();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_CyclicLocalFunctionSummaryIsIndependentOfFirstCall()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            private static bool GetFalse() => false;

            public static float Read(Command command)
            {
                void First(bool doWrite, bool callSecond)
                {
                    if (doWrite)
                        command = Command.Stop();
                    if (callSecond)
                        Second();
                }

                void Second() => First(true, false);

                First(false, GetFalse());
                if (!command.IsMove)
                    return 0;
                Second();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ConstructorPointerArgumentInvalidatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed unsafe class PointerMutator
            {
                public PointerMutator(Command* pointer)
                    => *pointer = Command.Stop();
            }
            """, """
            public static unsafe float Read(Command command)
            {
                Command* pointer = &command;
                if (!command.IsMove)
                    return 0;
                _ = new PointerMutator(pointer);
                return [|SPIRE014:command.move|].speed;
            }

            public static unsafe float ReadUnescaped(Command command, Command other)
            {
                Command* pointer = &other;
                if (!command.IsMove)
                    return 0;
                _ = new PointerMutator(pointer);
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ConstructorRunsPreviouslyEscapedCallback()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class Store
            {
                private global::System.Action _callback = null!;

                public void Register(global::System.Action callback)
                    => _callback = callback;

                public void Run()
                    => _callback();
            }

            public sealed class Runner
            {
                public Runner(Store store)
                    => store.Run();
            }
            """, """
            public static float Read(Command command, Store store)
            {
                store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                _ = new Runner(store);
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadUnescaped(Command command, Command other, Store store)
            {
                store.Register(() => other = Command.Stop());
                if (!command.IsMove)
                    return 0;
                _ = new Runner(store);
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_FunctionPointerRunsPreviouslyEscapedCallback()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class Store
            {
                private global::System.Action _callback = null!;

                public void Register(global::System.Action callback)
                    => _callback = callback;

                public void Run()
                    => _callback();
            }

            public static class FunctionPointerRunner
            {
                public static void Run(Store store)
                    => store.Run();
            }
            """, """
            public static unsafe float Read(Command command, Store store)
            {
                store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                delegate*<Store, void> pointer = &FunctionPointerRunner.Run;
                pointer(store);
                return [|SPIRE014:command.move|].speed;
            }

            public static unsafe float ReadUnescaped(Command command, Command other, Store store)
            {
                store.Register(() => other = Command.Stop());
                if (!command.IsMove)
                    return 0;
                delegate*<Store, void> pointer = &FunctionPointerRunner.Run;
                pointer(store);
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_OuterClosureComposesNestedCallbackAccessorEffects()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public sealed class Source
            {
                public global::System.Action Callback = null!;

                public int Value
                {
                    get
                    {
                        Callback();
                        return 0;
                    }
                }
            }
            """, """
            public static float Read(Command command, Source source)
            {
                global::System.Action outer = () =>
                {
                    source.Callback = () => command = Command.Stop();
                    _ = source.Value;
                };

                if (!command.IsMove)
                    return 0;
                outer();
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Pass_DeclaringNestedWriterWithoutInvokingOrEscapingDoesNotMutateCapture()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command)
            {
                global::System.Action outer = () =>
                {
                    global::System.Action inner = () => command = Command.Stop();
                };

                if (!command.IsMove)
                    return 0;
                outer();
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ClosureRefAliasWriteInvalidatesCapture()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static float Read(Command command)
            {
                global::System.Action mutate = () =>
                {
                    ref Command alias = ref command;
                    alias = Command.Stop();
                };

                if (!command.IsMove)
                    return 0;
                mutate();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadAliasOnly(Command command)
            {
                global::System.Func<Command.Kind> reader = () =>
                {
                    ref Command alias = ref command;
                    return alias.kind;
                };

                if (!command.IsMove)
                    return 0;
                _ = reader();
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_UserDeconstructionExecutesOpaqueEffects()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                public static Command Command;
            }

            public struct Pair
            {
                public void Deconstruct(out int first, out int second)
                {
                    first = 1;
                    second = 2;
                    Store.Command = Command.Stop();
                }
            }
            """, """
            public static float ReadAssignment()
            {
                Store.Command = Command.Move(1, 2);
                var pair = new Pair();
                if (!Store.Command.IsMove)
                    return 0;

                var (first, second) = pair;
                return [|SPIRE014:Store.Command.move|].speed;
            }

            public static float ReadPattern()
            {
                Store.Command = Command.Move(1, 2);
                var pair = new Pair();
                if (!Store.Command.IsMove)
                    return 0;

                if (pair is (var first, var second))
                    return [|SPIRE014:Store.Command.move|].speed;
                return 0;
            }

            public static float ReadBuiltinTuple()
            {
                Store.Command = Command.Move(1, 2);
                if (!Store.Command.IsMove)
                    return 0;

                var tuple = (1, 2);
                var (first, second) = tuple;
                return Store.Command.move.speed;
            }

            public static float ReadGeneratedPayload()
            {
                Store.Command = Command.Move(1, 2);
                if (!Store.Command.IsMove)
                    return 0;

                var (destination, speed) = Store.Command.move;
                return Store.Command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_UserPositionalDeconstructMutatesReceiver()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public void Deconstruct(out int value, out int other)
                {
                    value = 1;
                    other = 2;
                    this = Stop();
                }
            }
            """, """
            public static float ReadAssignment(Command command)
            {
                if (!command.IsMove)
                    return 0;
                var (value, other) = command;
                return command.move.speed;
            }

            public static float ReadPattern(Command command)
            {
                if (!command.IsMove)
                    return 0;
                if (command is (var value, var other))
                    return [|SPIRE014:command.move|].speed;
                return 0;
            }
            """));
    }

    [Fact]
    public async Task Error_UserOperatorsAndConversionsTransferCallbacks()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                private static global::System.Action _callback = null!;

                public static void Register(global::System.Action callback)
                    => _callback = callback;

                public static void Run()
                    => _callback();
            }

            public partial struct Command
            {
                public static Command operator -(Command value)
                {
                    Store.Run();
                    return value;
                }
            }

            public struct Wrapper
            {
                public static implicit operator Wrapper(Command value)
                {
                    Store.Run();
                    return new Wrapper();
                }

                public static Wrapper operator +(Command value, Wrapper other)
                {
                    Store.Run();
                    return other;
                }

                public static Wrapper operator +(Wrapper value, global::System.Action callback)
                {
                    callback();
                    return value;
                }

                public static implicit operator Wrapper(global::System.Action callback)
                {
                    callback();
                    return new Wrapper();
                }
            }
            """, """
            public static float ReadDirectConversion(Command command)
            {
                Store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                Wrapper value = command;
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadNestedConversion(Command command)
            {
                Store.Register(() => command = Command.Stop());
                global::System.Action mutate = () =>
                {
                    Wrapper value = command;
                };
                if (!command.IsMove)
                    return 0;
                mutate();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadBinaryOperator(Command command)
            {
                Store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                _ = command + default(Wrapper);
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadUnaryOperator(Command command)
            {
                Store.Register(() => command = Command.Stop());
                if (!command.IsMove)
                    return 0;
                _ = -command;
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadDirectDelegateOperator(Command command)
            {
                if (!command.IsMove)
                    return 0;
                _ = default(Wrapper) + (global::System.Action)(() => command = Command.Stop());
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadDirectDelegateConversion(Command command)
            {
                if (!command.IsMove)
                    return 0;
                Wrapper value = (global::System.Action)(() => command = Command.Stop());
                return [|SPIRE014:command.move|].speed;
            }
            """));
    }

    [Fact]
    public async Task Error_DelegateHeapStoresEscapeCallbacks()
    {
        await GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                public static global::System.Action Callback = null!;
                public static global::System.Action[] Slots = new global::System.Action[1];

                public static void Run()
                    => Callback();

                public static void RunSlot()
                    => Slots[0]();
            }
            """, """
            public static float ReadDirect(Command command)
            {
                Store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;
                Store.Run();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadArray(Command command)
            {
                Store.Slots[0] = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;
                Store.RunSlot();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadRefAlias(Command command)
            {
                ref var callback = ref Store.Callback;
                callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0;
                Store.Run();
                return [|SPIRE014:command.move|].speed;
            }

            public static float ReadStoreWithoutInvocation(Command command)
            {
                if (!command.IsMove)
                    return 0;
                Store.Callback = () => command = Command.Stop();
                return command.move.speed;
            }
            """));
    }

    [Fact]
    public async Task Error_ClosurePointerIndirectionWritesExposedStorage()
    {
        await GuardFlowTestHelper.CheckAsync(CommandScenario("""
            public static unsafe float Read(Command command)
            {
                Command* pointer = &command;
                global::System.Action mutate = () =>
                {
                    *pointer = Command.Stop();
                };
                if (!command.IsMove)
                    return 0;
                mutate();
                return [|SPIRE014:command.move|].speed;
            }

            public static unsafe float ReadIndexed(Command command)
            {
                Command* pointer = &command;
                global::System.Action mutate = () =>
                {
                    pointer[0] = Command.Stop();
                };
                if (!command.IsMove)
                    return 0;
                mutate();
                return [|SPIRE014:command.move|].speed;
            }

            public static unsafe float ReadUnrelated(Command command, Command other)
            {
                Command* pointer = &other;
                global::System.Action mutate = () =>
                {
                    *pointer = Command.Stop();
                };
                if (!command.IsMove)
                    return 0;
                mutate();
                return command.move.speed;
            }
            """));
    }

    private static string SourceWithExtension(string extension, string consumer)
        => """
            using System;
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }

            """ + extension + """

            public static class Scenario
            {
            """ + consumer + """
            }
            """;

    private static string CommandScenario(string body)
        => GuardFlowTestHelper.CommandSource("""
            public static class Scenario
            {
            """ + body + """
            }
            """);
}
