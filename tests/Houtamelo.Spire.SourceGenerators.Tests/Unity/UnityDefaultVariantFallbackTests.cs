using System;
using System.Reflection;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

[Trait("Environment", "Unity")]
public sealed class UnityDefaultVariantFallbackTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateDefault_ValidFirstSecondAndFieldlessVariantsUseFactories(bool includeEditorReferences)
    {
        var result = Run(DefaultUnionSource(), includeEditorReferences);
        AssertValid(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "valid default variant dispatch");
        var union = assembly.GetType("DefaultUnion", throwOnError: true)!;

        var first = InvokeDefault(union, 0);
        Assert.Equal("First", ReadKind(first));
        Assert.Equal(0, ReadPayloadField(first, "first", "value"));
        Assert.Null(ReadPayloadField(first, "first", "text"));

        var second = InvokeDefault(union, 1);
        Assert.Equal("Second", ReadKind(second));
        Assert.Equal(0f, ReadPayloadField(second, "second", "amount"));

        var empty = InvokeDefault(union, 2);
        Assert.Equal("Empty", ReadKind(empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateDefault_InvalidNegativeAndLargeInputsFallBackToFirst(bool includeEditorReferences)
    {
        var result = Run(DefaultUnionSource(), includeEditorReferences);
        AssertValid(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "invalid default variant dispatch");
        var union = assembly.GetType("DefaultUnion", throwOnError: true)!;

        foreach (var invalid in new[] { -1, -255, 256, 257, int.MinValue, int.MaxValue })
        {
            var fallback = InvokeDefault(union, invalid);
            Assert.Equal("First", ReadKind(fallback));
            Assert.Equal(0, ReadPayloadField(fallback, "first", "value"));
            Assert.Null(ReadPayloadField(fallback, "first", "text"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateDefault_FieldlessFirstVariant_InvalidInputsReturnEmpty(bool includeEditorReferences)
    {
        var result = Run(FieldlessFirstSource(), includeEditorReferences);
        AssertValid(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "fieldless first default variant dispatch");
        var union = assembly.GetType("FieldlessFirstUnion", throwOnError: true)!;

        foreach (var value in new[] { 0, -255, 257 })
        {
            var fallback = InvokeDefault(union, value);
            Assert.Equal("Empty", ReadKind(fallback));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateDefault_ClosedGenericAndNestedUnionsPreserveFallback(bool includeEditorReferences)
    {
        var result = Run(GenericAndNestedSource(), includeEditorReferences);
        AssertValid(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "generic and nested default variant dispatch");

        var generic = assembly.GetType("GenericCommand`1", throwOnError: true)!
            .MakeGenericType(typeof(int));
        var genericFallback = InvokeDefault(generic, 257);
        Assert.Equal("First", ReadKind(genericFallback));
        Assert.Equal(0, ReadPayloadField(genericFallback, "first", "value"));
        var genericSecond = InvokeDefault(generic, 1);
        Assert.Equal("Second", ReadKind(genericSecond));
        Assert.Equal(0f, ReadPayloadField(genericSecond, "second", "amount"));

        var nested = assembly.GetType("Outer+Command", throwOnError: true)!;
        var nestedFallback = InvokeDefault(nested, -255);
        Assert.Equal("First", ReadKind(nestedFallback));
        Assert.Equal(0, ReadPayloadField(nestedFallback, "first", "value"));
        var nestedSecond = InvokeDefault(nested, 1);
        Assert.Equal("Second", ReadKind(nestedSecond));
        Assert.Equal(0f, ReadPayloadField(nestedSecond, "second", "amount"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateDefault_DoesNotChangePayloadAuthoritativeFallback(bool includeEditorReferences)
    {
        var result = Run(DefaultUnionSource(), includeEditorReferences);
        AssertValid(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "serialized fallback remains authoritative");
        var union = assembly.GetType("DefaultUnion", throwOnError: true)!;

        var nullFallback = InvokeFromSerialized(union, null);
        Assert.Equal("First", ReadKind(nullFallback));
        Assert.Equal(0, ReadPayloadField(nullFallback, "first", "value"));
        Assert.Null(ReadPayloadField(nullFallback, "first", "text"));

        var second = InvokeFactory(union, "Second", 3.5f);
        var prepared = InvokeInstance(second, "__SpirePrepareForSerialization");
        var serializedPayload = FindField(prepared.GetType(), "_serializedPayload")!.GetValue(prepared);
        Assert.NotNull(serializedPayload);

        var restored = InvokeFromSerialized(union, serializedPayload);
        Assert.Equal("Second", ReadKind(restored));
        Assert.Equal(3.5f, ReadPayloadField(restored, "second", "amount"));

        var unknownFallback = InvokeFromSerialized(union, new object());
        Assert.Equal("First", ReadKind(unknownFallback));
        Assert.Equal(0, ReadPayloadField(unknownFallback, "first", "value"));
        Assert.Null(ReadPayloadField(unknownFallback, "first", "text"));
    }

    private static UnityGeneratorRun Run(string source, bool includeEditorReferences)
        => UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: true,
            includeEditorReferences: includeEditorReferences);

    private static void AssertValid(UnityGeneratorRun result)
    {
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
    }

    private static object InvokeDefault(Type union, int kind)
    {
        var method = union.GetMethod("__SpireCreateDefault", PublicStatic);
        Assert.NotNull(method);
        return method!.Invoke(null, new object[] { kind })!;
    }

    private static object InvokeFromSerialized(Type union, object? payload)
    {
        var method = union.GetMethod("__SpireFromSerialized", PublicStatic);
        Assert.NotNull(method);
        return method!.Invoke(null, new object?[] { payload })!;
    }

    private static object InvokeFactory(Type union, string name, params object?[] arguments)
    {
        var method = union.GetMethod(name, PublicStatic);
        Assert.NotNull(method);
        return method!.Invoke(null, arguments)!;
    }

    private static object InvokeInstance(object value, string name)
    {
        var method = value.GetType().GetMethod(name, AllInstance);
        Assert.NotNull(method);
        return method!.Invoke(value, null)!;
    }

    private static string ReadKind(object union)
        => union.GetType().GetProperty("kind", PublicInstance)!.GetValue(union)!.ToString()!;

    private static object? ReadPayloadField(object union, string accessorName, string fieldName)
    {
        var accessor = union.GetType().GetProperty(accessorName, PublicInstance);
        Assert.NotNull(accessor);
        var payload = accessor!.GetValue(union);
        Assert.NotNull(payload);

        var property = payload!.GetType().GetProperty(fieldName, PublicInstance);
        if (property is not null)
            return property.GetValue(payload);

        var field = FindField(payload.GetType(), "_" + fieldName) ??
            FindField(payload.GetType(), fieldName);
        Assert.NotNull(field);
        return field!.GetValue(payload);
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field is not null)
                return field;
        }

        return null;
    }

    private static string DefaultUnionSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct DefaultUnion
        {
            [Variant] public static partial DefaultUnion First(int value, string text);
            [Variant] public static partial DefaultUnion Second(float amount);
            [Variant] public static partial DefaultUnion Empty();
        }
        """;

    private static string FieldlessFirstSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct FieldlessFirstUnion
        {
            [Variant] public static partial FieldlessFirstUnion Empty();
            [Variant] public static partial FieldlessFirstUnion Later(int value);
        }
        """;

    private static string GenericAndNestedSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct GenericCommand<T>
        {
            [Variant] public static partial GenericCommand<T> First(T value);
            [Variant] public static partial GenericCommand<T> Second(float amount);
        }

        public static partial class Outer
        {
            [Serializable, DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command First(int value);
                [Variant] public static partial Command Second(float amount);
            }
        }
        """;
}
