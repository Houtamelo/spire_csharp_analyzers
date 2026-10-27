using System;
using System.Reflection;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public sealed class UnityRuntimeAssertionTests
{
    [Fact]
    public void UsableUnityDebugAssertIsCalledAtRuntimeWithoutAddingManagedStorage()
    {
        var result = Generate(UsableUnityDebugSource());
        var source = result.GeneratedSource;

        Assert.Contains("global::UnityEngine.Debug.Assert", source, StringComparison.Ordinal);
        Assert.DoesNotContain("global::System.Diagnostics.Debug.Assert", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Actual kind:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ISerializationCallbackReceiver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_serializedPayload", source, StringComparison.Ordinal);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "Unity runtime assertion stub");
        var probe = assembly.GetType("AssertionProbe", throwOnError: true)!;

        Assert.Equal(1, probe.GetMethod("ReadActive")!.Invoke(null, null));
        Assert.Equal(0, probe.GetMethod("ReadActiveFailedAssertions")!.Invoke(null, null));
        Assert.Equal(1, probe.GetMethod("ReadInactive")!.Invoke(null, null));
        Assert.Equal(1, probe.GetMethod("ReadInactiveFailedAssertions")!.Invoke(null, null));
        Assert.Contains(
            "Accessing the `Move` variant's fields is only allowed",
            (string)probe.GetMethod("ReadInactiveMessage")!.Invoke(null, null)!,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Actual kind:",
            (string)probe.GetMethod("ReadInactiveMessage")!.Invoke(null, null)!,
            StringComparison.Ordinal);

        var union = assembly.GetType("Command", throwOnError: true)!;
        var runtimeFields = union.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Contains(runtimeFields, field => field.FieldType.FullName == "CommandTypes+Fields+Unmanaged");
        Assert.DoesNotContain(runtimeFields, field => !field.FieldType.IsValueType);
    }

    [Fact]
    public void DotNetHostWithoutUnityUsesSystemDiagnosticsAssert()
    {
        var result = Generate(StandaloneSource());
        Assert.Contains("global::System.Diagnostics.Debug.Assert", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::UnityEngine.Debug.Assert", result.GeneratedSource, StringComparison.Ordinal);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "standalone runtime assertion");
        var union = assembly.GetType("Command", throwOnError: true)!;
        var value = union.GetMethod("Move")!.Invoke(null, new object[] { 7 })!;
        var payload = union.GetProperty("move")!.GetValue(value)!;
        Assert.Equal(7, payload.GetType().GetProperty("distance")!.GetValue(payload));
    }

    [Fact]
    public void PartialUnityDebugWithoutAssertFallsBackToSystemDiagnostics()
    {
        var result = Generate(PartialUnityDebugSource());
        Assert.Contains("global::System.Diagnostics.Debug.Assert", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::UnityEngine.Debug.Assert", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "partial Unity debug fallback");
        var union = assembly.GetType("Command", throwOnError: true)!;
        var value = union.GetMethod("Move")!.Invoke(null, new object[] { 3 })!;
        var payload = union.GetProperty("move")!.GetValue(value)!;
        Assert.Equal(3, payload.GetType().GetProperty("distance")!.GetValue(payload));
    }

    private static UnityGeneratorRun Generate(string source)
    {
        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: false);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return result;
    }

    private static string UsableUnityDebugSource() => """
        using Houtamelo.Spire;

        namespace UnityEngine
        {
            public static class Debug
            {
                public static int AssertCalls;
                public static int FailedAssertions;
                public static string LastMessage = string.Empty;

                public static void Reset()
                {
                    AssertCalls = 0;
                    FailedAssertions = 0;
                    LastMessage = string.Empty;
                }

                public static void Assert(bool condition, string message)
                {
                    AssertCalls++;
                    if (!condition)
                    {
                        FailedAssertions++;
                        LastMessage = message;
                    }
                }
            }
        }

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Idle();
            [Variant] public static partial Command Move(int distance);
        }

        public static class AssertionProbe
        {
            public static int ReadActive()
            {
                UnityEngine.Debug.Reset();
                _ = Command.Move(7).move.distance;
                return UnityEngine.Debug.AssertCalls;
            }

            public static int ReadActiveFailedAssertions()
                => UnityEngine.Debug.FailedAssertions;

            public static int ReadInactive()
            {
                UnityEngine.Debug.Reset();
                _ = Command.Idle().move.distance;
                return UnityEngine.Debug.AssertCalls;
            }

            public static int ReadInactiveFailedAssertions()
                => UnityEngine.Debug.FailedAssertions;

            public static string ReadInactiveMessage()
                => UnityEngine.Debug.LastMessage;
        }
        """;

    private static string StandaloneSource() => """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Idle();
            [Variant] public static partial Command Move(int distance);
        }
        """;

    private static string PartialUnityDebugSource() => """
        using Houtamelo.Spire;

        namespace UnityEngine
        {
            public static class Debug
            {
                public static void Log(string message) { }
            }
        }

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Idle();
            [Variant] public static partial Command Move(int distance);
        }
        """;
}
