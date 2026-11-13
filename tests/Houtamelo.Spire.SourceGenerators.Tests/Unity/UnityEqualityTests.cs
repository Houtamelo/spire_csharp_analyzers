using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Unity-backed equality coverage for serialized unions and former payload aliases.
[Trait("Environment", "Unity")]
public sealed class UnityEqualityTests
{
    [Fact]
    public void SerializedPayloadAndCacheState_DoNotParticipateInEquality()
    {
        var result = UnityGeneratorTestHarness.Run(SerializableSource(), includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var unionSymbol = result.OutputCompilation.GetTypeByMetadataName("SerializableState")!;
        Assert.Contains(unionSymbol.AllInterfaces, type => type.Name == "IEquatable" && type.TypeArguments[0].Name == "SerializableState");

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "serialized union equality");
        var union = assembly.GetType("SerializableState", throwOnError: true)!;
        var first = InvokeFactory(union, "First", 42);
        var prepared = InvokeInstance(first, "__SpirePrepareForSerialization");
        var hydrated = InvokeInstance(prepared, "__SpireHydrateSerialized");
        var hydratedAgain = InvokeInstance(hydrated, "__SpireHydrateSerialized");
        var changed = InvokeFactory(union, "First", 43);
        var other = InvokeFactory(union, "Second", "same cache");
        var staleSource = InvokeInstance(
            InvokeFactory(union, "First", 999),
            "__SpirePrepareForSerialization");
        var stalePayload = GetSerializedPayload(union, staleSource);
        Assert.NotNull(stalePayload);
        var stale = InvokeFactory(union, "First", 42);
        SetSerializedPayload(union, stale, stalePayload);
        SetSerializedPayload(union, changed, stalePayload);

        Assert.True(TypedEquals(union, first, prepared));
        Assert.True(TypedEquals(union, prepared, hydrated));
        Assert.True(TypedEquals(union, hydrated, hydratedAgain));
        Assert.True(TypedEquals(union, first, stale));
        Assert.True(OperatorEquals(union, first, stale));
        Assert.True(DefaultEquals(union, first, stale));
        Assert.True(OperatorEquals(union, first, hydrated));
        Assert.True(DefaultEquals(union, first, hydrated));
        Assert.Equal(first.GetHashCode(), prepared.GetHashCode());
        Assert.Equal(first.GetHashCode(), hydrated.GetHashCode());
        Assert.Equal(first.GetHashCode(), stale.GetHashCode());

        Assert.False(TypedEquals(union, first, changed));
        Assert.False(OperatorEquals(union, first, changed));
        Assert.False(TypedEquals(union, first, other));
        Assert.False(OperatorEquals(union, first, other));
    }

    [Fact]
    public void FormerSerializedPayloadAlias_DoesNotReceiveCanonicalEqualitySurface()
    {
        var result = UnityGeneratorTestHarness.Run(
            AliasSource(),
            includeUnityReferences: true,
            includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("AliasedState")!;
        var canonical = result.OutputCompilation.GetTypeByMetadataName("AliasedStateTypes+Variants+Current")!;
        var former = result.OutputCompilation.GetTypeByMetadataName("AliasedStateTypes+Variants+Old")!;
        Assert.Contains(union.AllInterfaces, type => type.Name == "IEquatable" &&
            SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], canonical));
        Assert.DoesNotContain(union.AllInterfaces, type => type.Name == "IEquatable" &&
            SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], former));
        Assert.DoesNotContain(union.GetMembers("op_Equality").OfType<IMethodSymbol>(), method =>
            method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, former)));
    }

    private static object InvokeFactory(Type union, string name, params object?[] arguments)
        => union.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static object InvokeInstance(object value, string name)
        => value.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(value, null)!;

    private static object? GetSerializedPayload(Type union, object value)
        => union.GetField("_serializedPayload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(value);

    private static void SetSerializedPayload(Type union, object value, object? payload)
        => union.GetField("_serializedPayload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(value, payload);

    private static bool TypedEquals(Type union, object left, object right)
        => (bool)union.GetMethod("Equals", new[] { union })!.Invoke(left, new[] { right })!;

    private static bool OperatorEquals(Type union, object left, object right)
        => (bool)union.GetMethod("op_Equality", new[] { union, union })!.Invoke(null, new[] { left, right })!;

    private static bool DefaultEquals(Type union, object left, object right)
    {
        var comparerType = typeof(EqualityComparer<>).MakeGenericType(union);
        var comparer = comparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return (bool)comparerType.GetMethod("Equals", new[] { union, union })!.Invoke(comparer, new[] { left, right })!;
    }

    private static string SerializableSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct SerializableState
        {
            [Variant] public static partial SerializableState First(int value);
            [Variant] public static partial SerializableState Second(string text);
        }
        """;

    private static string AliasSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct AliasedState
        {
            [Variant(FormerlySerializedAs = "Old")]
            public static partial AliasedState Current(int value);
            [Variant] public static partial AliasedState Empty();
        }
        """;
}
