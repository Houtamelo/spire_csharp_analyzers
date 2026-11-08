using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

/// Regression coverage for nested CFG scopes and loop-carried receiver paths.
public sealed class GuardModelRegressionTests
{
    [Fact]
    public Task Pass_NestedLocalFunctionOwnGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float Outer()
                    {
                        float Inner()
                        {
                            if (!command.IsMove)
                                return 0f;
                            return command.move.speed;
                        }

                        return Inner();
                    }

                    return Outer();
                }
            }
            """));

    [Fact]
    public Task Error_NestedLocalFunctionDoesNotInheritOuterGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!command.IsMove)
                        return 0f;

                    float Outer()
                    {
                        float Inner() => [|SPIRE014:command.move|].speed;
                        return Inner();
                    }

                    return Outer();
                }
            }
            """));

    [Fact]
    public Task Pass_LocalFunctionCallingLambdaWithOwnGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float Outer()
                    {
                        System.Func<float> inner = () =>
                        {
                            if (!command.IsMove)
                                return 0f;
                            return command.move.speed;
                        };

                        return inner();
                    }

                    return Outer();
                }
            }
            """));

    [Fact]
    public Task Error_LambdaDoesNotInheritOuterGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!command.IsMove)
                        return 0f;

                    float Outer()
                    {
                        System.Func<float> inner = () => [|SPIRE014:command.move|].speed;
                        return inner();
                    }

                    return Outer();
                }
            }
            """));

    [Fact]
    public Task Pass_LambdaCallingLocalFunctionWithOwnGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float Inner()
                    {
                        if (!command.IsMove)
                            return 0f;
                        return command.move.speed;
                    }

                    System.Func<float> outer = () => Inner();
                    return outer();
                }
            }
            """));

    [Fact]
    public Task Error_LambdaCallingLocalFunctionWithoutGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float Inner() => [|SPIRE014:command.move|].speed;
                    System.Func<float> outer = () => Inner();
                    return outer();
                }
            }
            """));

    [Fact]
    public Task Pass_ThreeNestedLocalFunctionsGuardAtInnermostScope()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float First()
                    {
                        float Second()
                        {
                            float Third()
                            {
                                if (!command.IsMove)
                                    return 0f;
                                return command.move.speed;
                            }

                            return Third();
                        }

                        return Second();
                    }

                    return First();
                }
            }
            """));

    [Fact]
    public Task Error_ThreeNestedLocalFunctionsDoNotShareOuterGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    if (!command.IsMove)
                        return 0f;

                    float First()
                    {
                        float Second()
                        {
                            float Third() => [|SPIRE014:command.move|].speed;
                            return Third();
                        }

                        return Second();
                    }

                    return First();
                }
            }
            """));

    [Fact]
    public Task Pass_SiblingNestedFunctionsKeepIndependentGuards()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float guarded()
                    {
                        if (!command.IsMove)
                            return 0f;
                        return command.move.speed;
                    }

                    float empty() => 0f;
                    return guarded() + empty();
                }
            }
            """));

    [Fact]
    public Task Error_SiblingNestedFunctionsReportOnlyUnguardedAccessor()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    float guarded()
                    {
                        if (!command.IsMove)
                            return 0f;
                        return command.move.speed;
                    }

                    float unguarded() => [|SPIRE014:command.move|].speed;
                    return guarded() + unguarded();
                }
            }
            """));

    [Fact]
    public Task Pass_RecursiveNestedFunctionsGuardAtBaseCase()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, int depth)
                {
                    float First(int value)
                    {
                        if (value == 0)
                        {
                            if (!command.IsMove)
                                return 0f;
                            return command.move.speed;
                        }

                        return Second(value - 1);
                    }

                    float Second(int value) => First(value);
                    return First(depth);
                }
            }
            """));

    [Fact]
    public Task Error_RecursiveNestedFunctionsReportUnguardedBaseCase()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command, int depth)
                {
                    float First(int value)
                    {
                        if (value == 0)
                            return [|SPIRE014:command.move|].speed;
                        return Second(value - 1);
                    }

                    float Second(int value) => First(value);
                    return First(depth);
                }
            }
            """));

    [Fact]
    public Task Pass_RefReceiverLoopRechecksGuardAfterRebinding()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Cell
            {
                public Cell Next = new();
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Cell root, int count)
                {
                    ref Cell cursor = ref root;
                    var result = 0f;
                    for (var index = 0; index < count; index++)
                    {
                        cursor = ref cursor.Next;
                        if (!cursor.Command.IsMove)
                            continue;
                        result += cursor.Command.move.speed;
                    }

                    return result;
                }
            }
            """));

    [Fact]
    public Task Error_RefReceiverLoopCannotReuseGuardBeforeRebinding()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            sealed class Cell
            {
                public Cell Next = new();
                public Command Command = Command.Stop();
            }

            static class Reader
            {
                public static float Read(Cell root, int count)
                {
                    ref Cell cursor = ref root;
                    if (!cursor.Command.IsMove)
                        return 0f;

                    for (var index = 0; index < count; index++)
                        cursor = ref cursor.Next;

                    return [|SPIRE014:cursor.Command.move|].speed;
                }
            }
            """));

    [Fact]
    public Task Error_RefReturnSpanWriteInvalidatesRootGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var values = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref command, 1);
                    if (command.IsMove)
                    {
                        values[0] = Command.Stop();
                        return [|SPIRE014:command.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_RefReturnSpanWriteDoesNotInvalidateValueCopy()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var snapshot = command;
                    var values = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref command, 1);
                    if (snapshot.IsMove)
                    {
                        values[0] = Command.Stop();
                        return snapshot.move.speed;
                    }

                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_RefReturnSpanWriteAllowsFreshRootGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    var values = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref command, 1);
                    values[0] = Command.Stop();
                    if (command.IsMove)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_DefiniteRefAliasGuardProvesOriginalReceiver()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    ref Command alias = ref command;
                    if (alias.IsMove)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Pass_ChainedDefiniteRefAliasesProveOriginalReceiver()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    ref Command first = ref command;
                    ref Command second = ref first;
                    if (second.IsMove)
                        return command.move.speed;
                    return 0f;
                }
            }
            """));

    [Fact]
    public Task Error_OriginalReceiverWriteInvalidatesDefiniteRefAliasGuard()
        => GuardFlowTestHelper.CheckAsync(GuardFlowTestHelper.CommandSource("""
            static class Reader
            {
                public static float Read(Command command)
                {
                    ref Command alias = ref command;
                    if (alias.IsMove)
                    {
                        command = Command.Stop();
                        return [|SPIRE014:alias.move|].speed;
                    }

                    return 0f;
                }
            }
            """));

}
