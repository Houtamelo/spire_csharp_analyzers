using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

/// Regression coverage for exposed byref storage, suspension, impure patterns,
/// and constant CFG edges.
public sealed class GuardModelEffectRegressionTests
{
    [Fact]
    public Task Error_PointerStoredIntoStaticFieldCanMutateLocal()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;

                public static void Replace()
                    => *Pointer = Command.Stop();
            }
            """, """
            public static unsafe float Read(Command command)
            {
                Store.Pointer = &command;
                if (!command.IsMove)
                    return 0f;

                Store.Replace();
                return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Pass_PointerToUnrelatedLocalDoesNotInvalidateReceiver()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;

                public static void Replace()
                    => *Pointer = Command.Stop();
            }
            """, """
            public static unsafe float Read(Command command, Command other)
            {
                Store.Pointer = &other;
                if (!command.IsMove)
                    return 0f;

                Store.Replace();
                return command.move.speed;
            }
            """));

    [Fact]
    public Task Error_ConstructorCanWriteEscapedPointer()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;
            }

            public unsafe struct PointerWriter
            {
                public PointerWriter(int marker)
                {
                    _ = marker;
                    *Store.Pointer = Command.Stop();
                }
            }
            """, """
            public static unsafe float Read(Command command)
            {
                Store.Pointer = &command;
                if (!command.IsMove)
                    return 0f;

                _ = new PointerWriter(1);
                return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Error_UserOperatorCanWriteEscapedPointer()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;
            }

            public partial struct Command
            {
                public static unsafe Command operator +(Command value, int amount)
                {
                    _ = amount;
                    *Store.Pointer = Stop();
                    return value;
                }
            }
            """, """
            public static unsafe float Read(Command command)
            {
                Store.Pointer = &command;
                if (!command.IsMove)
                    return 0f;

                _ = command + 1;
                return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Error_UnrelatedPropertyGetterCanWriteEscapedPointer()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;
            }

            public static unsafe class UnrelatedReader
            {
                public static int Trigger
                {
                    get
                    {
                        *Store.Pointer = Command.Stop();
                        return 0;
                    }
                }
            }
            """, """
            public static unsafe float Read(Command command)
            {
                Store.Pointer = &command;
                if (!command.IsMove)
                    return 0f;

                _ = UnrelatedReader.Trigger;
                return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Pass_OpaquePointerWriterCannotReachByValueSnapshot()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static unsafe class Store
            {
                public static Command* Pointer;
            }

            public unsafe struct PointerWriter
            {
                public PointerWriter(int marker)
                {
                    _ = marker;
                    *Store.Pointer = Command.Stop();
                }
            }
            """, """
            public static unsafe float Read(Command command, Command other)
            {
                Store.Pointer = &other;
                var snapshot = command;
                if (!snapshot.IsMove)
                    return 0f;

                _ = new PointerWriter(1);
                return snapshot.move.speed;
            }
            """));

    [Fact]
    public Task Error_EscapedCallbackCanRunBetweenIteratorResumptions()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                public static System.Action? Callback;
            }
            """, """
            public static System.Collections.Generic.IEnumerable<float> Read(Command command)
            {
                Store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    yield break;

                yield return 0f;
                yield return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Pass_UnescapedValueLocalSurvivesIteratorSuspension()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static System.Collections.Generic.IEnumerable<float> Read(Command command)
                {
                    var snapshot = command;
                    if (!snapshot.IsMove)
                        yield break;

                    yield return snapshot.move.speed;
                    yield return snapshot.move.speed;
                }
            }
            """));

    [Fact]
    public Task Error_ImpurePropertyPatternCannotProvePayload()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public bool Trigger
                {
                    get
                    {
                        this = Stop();
                        return true;
                    }
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (command is { IsMove: true, Trigger: true, [|SPIRE014:move|]: var payload })
                    return payload.speed;

                return 0f;
            }
            """));

    [Fact]
    public Task Error_ImpurePropertyPatternInvalidatesOuterGuard()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public partial struct Command
            {
                public bool Trigger
                {
                    get
                    {
                        this = Stop();
                        return true;
                    }
                }
            }
            """, """
            public static float Read(Command command)
            {
                if (!command.IsMove)
                    return 0f;

                if (command is { Trigger: true, [|SPIRE014:move|]: var payload })
                    return payload.speed;

                return 0f;
            }
            """));

    [Fact]
    public Task Pass_PureGeneratedPropertyPatternProvesPayload()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command is { kind: Command.Kind.Move, move: var payload })
                        return payload.speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnreachableConstantBranchesDoNotReport()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (false)
                        return command.move.speed;

                    if (command.IsMove && false)
                        return command.move.speed;

                    if (false || command.IsMove)
                        return command.move.speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_TrueAndDiscriminatorGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (true && command.IsMove)
                        return command.move.speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ContradictoryDiscriminatorBranchIsUnreachable()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove && !command.IsMove)
                        return command.move.speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OrTrueDoesNotGuardPayload()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (command.IsMove || true)
                        return [|SPIRE014:command.move|].speed;

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_UnknownDelegateWithoutEscapedCallback()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, System.Action external)
                {
                    if (!command.IsMove)
                        return 0f;

                    external();
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Error_DynamicParameterCanInvokeEscapedCallback()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                public static System.Action? Callback;
            }
            """, """
            public static float Read(Command command, dynamic receiver)
            {
                Store.Callback = () => command = Command.Stop();
                if (!command.IsMove)
                    return 0f;

                receiver.Run();
                return [|SPIRE014:command.move|].speed;
            }
            """));

    [Fact]
    public Task Pass_DynamicParameterCannotReachUnescapedValue()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, dynamic receiver)
                {
                    if (!command.IsMove)
                        return 0f;

                    receiver.Run();
                    return command.move.speed;
                }
            }
            """));

    [Fact]
    public Task Pass_DynamicParameterEscapedCallbackTargetsOtherReceiver()
        => GuardFlowTestHelper.CheckAsync(SourceWithExtension("""
            public static class Store
            {
                public static System.Action? Callback;
            }
            """, """
            public static float Read(Command command, Command other, dynamic receiver)
            {
                Store.Callback = () => other = Command.Stop();
                if (!command.IsMove)
                    return 0f;

                receiver.Run();
                return command.move.speed;
            }
            """));

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
}
