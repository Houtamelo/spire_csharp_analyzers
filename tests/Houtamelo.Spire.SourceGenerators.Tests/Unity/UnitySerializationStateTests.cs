using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Regression coverage for the single per-instance Unity serialization field.
public sealed class UnitySerializationStateTests
{
    [Fact]
    public void SerializableUnionAddsOnlySerializedPayloadToTheRuntimeInstanceState()
    {
        var result = Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var serialized = RequireType(result, "SerializedState");
        var plain = RequireType(result, "PlainState");
        var serializedNames = InstanceFieldNames(serialized);
        var plainNames = InstanceFieldNames(plain);

        Assert.Equal(new[] { "_serializedPayload" }, serializedNames.Except(plainNames).ToArray());
        Assert.DoesNotContain("__SpireSerializationHydrated", serializedNames);
        Assert.DoesNotContain(
            result.GeneratedSource,
            "__SpireSerializationHydrated",
            StringComparison.Ordinal);
    }

    [Fact]
    public void SharedMarkerIsGeneratedOnceAndHydrationReusesItAcrossCopiesAndGenericUnions()
    {
        var result = Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payloadBase = result.OutputCompilation.GetTypeByMetadataName(
            "Houtamelo.Spire.UnitySerializationPayloadBase");
        Assert.NotNull(payloadBase);
        var markerTypes = AllNamedTypes(result.OutputCompilation)
            .Where(type => type.TypeParameters.Length == 0 &&
                !SymbolEqualityComparer.Default.Equals(type, payloadBase) &&
                DerivesFrom(type, payloadBase!) &&
                type.GetMembers().OfType<IFieldSymbol>().Any(field => field.IsStatic &&
                    field.IsReadOnly &&
                    SymbolEqualityComparer.Default.Equals(field.Type, type)))
            .ToArray();
        Assert.Single(markerTypes);
        var markerType = markerTypes[0];
        var markerFields = markerType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.IsStatic &&
                SymbolEqualityComparer.Default.Equals(field.Type, markerType))
            .ToArray();
        Assert.Single(markerFields);
        Assert.True(markerFields[0].IsReadOnly);
        foreach (var unionName in new[] { "SerializedState", "OtherState", "GenericState`1", "Host+NestedState" })
        {
            var union = result.OutputCompilation.GetTypeByMetadataName(unionName);
            Assert.NotNull(union);
            var payloadField = union!.GetMembers("_serializedPayload")
                .OfType<IFieldSymbol>()
                .Single();
            Assert.True(SymbolEqualityComparer.Default.Equals(payloadField.Type, payloadBase));
        }
        foreach (var unionName in new[] { "SerializedState", "OtherState", "GenericState`1", "Host+NestedState" })
        {
            var union = result.OutputCompilation.GetTypeByMetadataName(unionName);
            Assert.NotNull(union);
            Assert.NotEmpty(union!.GetMembers("__SpireHydrateSerialized"));
            Assert.NotEmpty(union.GetMembers("__SpireFromSerialized"));
        }

        var assembly = LoadUnityGeneratedAssembly(result.OutputCompilation);
        var stateType = assembly.GetType("SerializedState", throwOnError: true)!;
        var otherType = assembly.GetType("OtherState", throwOnError: true)!;
        var genericType = assembly.GetType("GenericState`1", throwOnError: true)!
            .MakeGenericType(typeof(int));
        var nestedType = assembly.GetType("Host+NestedState", throwOnError: true)!;

        var state = InvokeFactory(stateType, "First", 42);
        Assert.Null(GetSerializedPayload(stateType, state));

        var prepared = InvokeInstance(state, "__SpirePrepareForSerialization");
        var preparedPayload = GetSerializedPayload(stateType, prepared);
        Assert.NotNull(preparedPayload);
        Assert.Equal(42, ReadPayload(prepared, "first", "value"));

        var hydrated = InvokeInstance(prepared, "__SpireHydrateSerialized");
        var marker = GetSerializedPayload(stateType, hydrated);
        Assert.NotNull(marker);
        Assert.Equal("First", stateType.GetProperty("kind")!.GetValue(hydrated)!.ToString());
        Assert.Equal(42, ReadPayload(hydrated, "first", "value"));

        var rehydrated = InvokeInstance(hydrated, "__SpireHydrateSerialized");
        Assert.Same(marker, GetSerializedPayload(stateType, rehydrated));
        Assert.Equal(42, ReadPayload(rehydrated, "first", "value"));

        var otherPrepared = InvokeInstance(
            InvokeFactory(otherType, "First", 7), "__SpirePrepareForSerialization");
        var other = InvokeInstance(otherPrepared, "__SpireHydrateSerialized");
        Assert.Same(marker, GetSerializedPayload(otherType, other));
        Assert.Equal(7, ReadPayload(other, "first", "value"));

        var genericPrepared = InvokeInstance(
            InvokeFactory(genericType, "First", 11), "__SpirePrepareForSerialization");
        var generic = InvokeInstance(genericPrepared, "__SpireHydrateSerialized");
        Assert.Same(marker, GetSerializedPayload(genericType, generic));
        Assert.Equal(11, ReadPayload(generic, "first", "value"));

        var nestedPrepared = InvokeInstance(
            InvokeFactory(nestedType, "First", 13), "__SpirePrepareForSerialization");
        var nested = InvokeInstance(nestedPrepared, "__SpireHydrateSerialized");
        Assert.Same(marker, GetSerializedPayload(nestedType, nested));
        Assert.Equal(13, ReadPayload(nested, "first", "value"));

        Assert.Same(marker, GetSerializedPayload(stateType, RuntimeHelpers.GetObjectValue(hydrated)));

        Assert.NotSame(marker, preparedPayload);
        Assert.NotEqual(marker!.GetType(), preparedPayload!.GetType());

        var fallback = stateType.GetMethod(
                "__SpireFromSerialized",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, new[] { marker })!;
        Assert.Equal("First", stateType.GetProperty("kind")!.GetValue(fallback)!.ToString());
        Assert.Equal(0, ReadPayload(fallback, "first", "value"));
    }

    [Fact]
    public void SerializedPayloadRestoresCurrentFormerAndInvalidValues()
    {
        var result = Run(AliasSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = LoadUnityGeneratedAssembly(result.OutputCompilation);
        var stateType = assembly.GetType("AliasState", throwOnError: true)!;
        var currentPayloadType = assembly.GetType("AliasStateTypes+Variants+Current", throwOnError: true)!;
        var formerPayloadType = assembly.GetType("AliasStateTypes+Variants+Old", throwOnError: true)!;

        var currentPayload = Activator.CreateInstance(currentPayloadType, new object[] { 42 })!;
        var currentWrapper = WrapPayload(assembly, currentPayloadType, currentPayload);
        var fromSerialized = stateType.GetMethod(
            "__SpireFromSerialized",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        var restoredCurrent = fromSerialized.Invoke(null, new[] { currentWrapper })!;
        Assert.Equal("Current", stateType.GetProperty("kind")!.GetValue(restoredCurrent)!.ToString());
        Assert.Equal(42, ReadPayload(restoredCurrent, "current", "value"));

        var formerPayload = Activator.CreateInstance(formerPayloadType, new object[] { 17 })!;
        var formerWrapper = WrapPayload(assembly, formerPayloadType, formerPayload);
        var restoredFormer = fromSerialized.Invoke(null, new[] { formerWrapper })!;
        Assert.Equal("Current", stateType.GetProperty("kind")!.GetValue(restoredFormer)!.ToString());
        Assert.Equal(17, ReadPayload(restoredFormer, "current", "value"));

        var restoredInvalid = fromSerialized.Invoke(null, new object[] { new object() })!;
        Assert.Equal("Current", stateType.GetProperty("kind")!.GetValue(restoredInvalid)!.ToString());
        Assert.Equal(0, ReadPayload(restoredInvalid, "current", "value"));

        var restoredNull = fromSerialized.Invoke(null, new object?[] { null })!;
        Assert.Equal("Current", stateType.GetProperty("kind")!.GetValue(restoredNull)!.ToString());
        Assert.Equal(0, ReadPayload(restoredNull, "current", "value"));
    }

    [Fact]
    public void SerializedStateCurrentUsesTupleSwitchAndDistinguishesPayloadKindsAndValues()
    {
        var result = Run(TupleStateSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var generatedTree = result.RunResult.GeneratedTrees.Single(tree =>
            tree.GetText().ToString().Contains("__SpireSerializedStateCurrent(object? payload)", StringComparison.Ordinal));
        var method = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == "__SpireSerializedStateCurrent" &&
                candidate.ParameterList.Parameters.Count == 1);
        Assert.NotNull(method.ExpressionBody);
        Assert.IsType<SwitchExpressionSyntax>(method.ExpressionBody!.Expression);

        var assembly = LoadUnityGeneratedAssembly(result.OutputCompilation);
        var unionType = assembly.GetType("TupleState", throwOnError: true)!;
        var firstPayloadType = assembly.GetType("TupleStateTypes+Variants+First", throwOnError: true)!;
        var secondPayloadType = assembly.GetType("TupleStateTypes+Variants+Second", throwOnError: true)!;
        var nodePayloadType = assembly.GetType("TupleStateTypes+Variants+Node", throwOnError: true)!;

        var first = InvokeFactory(unionType, "First", 17);
        var firstPayload = Activator.CreateInstance(firstPayloadType, new object[] { 17 })!;
        var firstLegacyWrapper = WrapPayload(assembly, firstPayloadType, firstPayload);
        Assert.False(SerializedStateCurrent(unionType, first, firstLegacyWrapper));
        var firstWrapper = WrapConcretePayload(
            assembly,
            "First",
            firstPayload);
        Assert.True(SerializedStateCurrent(unionType, first, firstWrapper));
        Assert.False(SerializedStateCurrent(unionType, first, null));
        Assert.False(SerializedStateCurrent(unionType, first, new object()));

        var second = InvokeFactory(unionType, "Second", "hello");
        var secondPayload = Activator.CreateInstance(secondPayloadType, new object[] { "hello" })!;
        var secondWrapper = WrapConcretePayload(
            assembly,
            "Second",
            secondPayload);
        Assert.True(SerializedStateCurrent(unionType, second, secondWrapper));
        Assert.False(SerializedStateCurrent(
            unionType,
            second,
            WrapPayload(assembly, secondPayloadType, secondPayload)));
        var differentText = Activator.CreateInstance(secondPayloadType, new object[] { "other" })!;
        Assert.False(SerializedStateCurrent(
            unionType,
            second,
            WrapPayload(assembly, secondPayloadType, differentText)));
        Assert.False(SerializedStateCurrent(unionType, first, secondWrapper));

        var node = Activator.CreateInstance(assembly.GetType("TupleNode", throwOnError: true)!)!;
        var nodeState = InvokeFactory(unionType, "Node", node);
        var nodePayload = Activator.CreateInstance(nodePayloadType, new[] { node })!;
        var nodeWrapper = WrapConcretePayload(
            assembly,
            "Node",
            nodePayload);
        Assert.True(SerializedStateCurrent(unionType, nodeState, nodeWrapper));
        Assert.False(SerializedStateCurrent(
            unionType,
            nodeState,
            WrapPayload(assembly, nodePayloadType, nodePayload)));
        var otherNode = Activator.CreateInstance(assembly.GetType("TupleNode", throwOnError: true)!)!;
        var otherNodePayload = Activator.CreateInstance(nodePayloadType, new[] { otherNode })!;
        Assert.False(SerializedStateCurrent(
            unionType,
            nodeState,
            WrapPayload(assembly, nodePayloadType, otherNodePayload)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedUnityCallbacksExecuteInPlayerAndEditorCompilations(bool editorCompilation)
    {
        var result = UnityGeneratorTestHarness.Run(
            Source(),
            includeEditorReferences: editorCompilation);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = LoadUnityGeneratedAssembly(result.OutputCompilation);
        var stateType = assembly.GetType("SerializedState", throwOnError: true)!;
        var state = InvokeFactory(stateType, "First", 42);
        var prepared = InvokeInstance(state, "__SpirePrepareForSerialization");
        var payload = GetSerializedPayload(stateType, prepared);
        Assert.NotNull(payload);
        Assert.Equal(42, ReadPayload(prepared, "first", "value"));

        InvokeSerializationCallback(prepared, "OnAfterDeserialize");

        Assert.Equal(42, ReadPayload(prepared, "first", "value"));
        var afterDeserializePayload = GetSerializedPayload(stateType, prepared);
        Assert.NotNull(afterDeserializePayload);
        if (editorCompilation)
            Assert.Same(payload, afterDeserializePayload);
        else
            Assert.NotSame(payload, afterDeserializePayload);
    }

    private static UnityGeneratorRun Run(string source)
        => UnityGeneratorTestHarness.Run(source);

    private static INamedTypeSymbol RequireType(UnityGeneratorRun result, string metadataName)
        => Assert.IsAssignableFrom<INamedTypeSymbol>(
            result.OutputCompilation.GetTypeByMetadataName(metadataName));

    private static Assembly LoadUnityGeneratedAssembly(Compilation compilation)
        => UnityGeneratorTestHarness.EmitAndLoad(compilation, "Unity serialization state runtime");

    private static object InvokeFactory(Type unionType, string name, params object?[] arguments)
        => unionType.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static object InvokeInstance(object value, string name)
        => value.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(value, null)!;

    private static object? GetSerializedPayload(Type unionType, object value)
        => unionType.GetField("_serializedPayload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(value);

    private static bool SerializedStateCurrent(Type unionType, object union, object? payload)
        => (bool)unionType.GetMethod(
            "__SpireSerializedStateCurrent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            new[] { typeof(object) },
            modifiers: null)!.Invoke(union, new[] { payload })!;

    private static object WrapPayload(Assembly assembly, Type payloadType, object payload)
    {
        var wrapperDefinition = assembly.GetType(
            "Houtamelo.Spire.Generated.SpireSerializedPayload`1",
            throwOnError: true)!;
        var wrapperType = wrapperDefinition.MakeGenericType(payloadType);
        var wrapper = Activator.CreateInstance(wrapperType)!;
        wrapperType.GetField("value", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(wrapper, payload);
        return wrapper;
    }

    private static object WrapConcretePayload(
        Assembly assembly,
        string variantName,
        object payload)
    {
        var wrapperType = assembly.GetType(
            "TupleStateTypes+SerializedPayloads+" + variantName + "SerializedPayload",
            throwOnError: true)!;
        var wrapper = Activator.CreateInstance(wrapperType)!;
        wrapperType.GetField("value", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(wrapper, payload);
        return wrapper;
    }

    private static void InvokeSerializationCallback(object value, string name)
    {
        var callbackInterface = value.GetType().GetInterfaces()
            .Single(interfaceType =>
                interfaceType.FullName == "UnityEngine.ISerializationCallbackReceiver");
        callbackInterface.GetMethod(name)!.Invoke(value, null);
    }

    private static object? ReadPayload(object union, string accessor, string field)
    {
        var payload = union.GetType().GetProperty(accessor)!.GetValue(union)!;
        return payload.GetType().GetProperty(field)!.GetValue(payload);
    }

    private static string[] InstanceFieldNames(INamedTypeSymbol type)
        => type.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static INamedTypeSymbol[] AllNamedTypes(Compilation compilation)
        => FlattenNamespace(compilation.Assembly.GlobalNamespace)
            .SelectMany(FlattenTypes)
            .ToArray();

    private static IEnumerable<INamedTypeSymbol> FlattenNamespace(INamespaceSymbol symbol)
    {
        foreach (var type in symbol.GetTypeMembers())
            yield return type;
        foreach (var child in symbol.GetNamespaceMembers())
            foreach (var type in FlattenNamespace(child))
                yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> FlattenTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers().SelectMany(FlattenTypes))
            yield return nested;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return false;
    }

    private static string Source() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct SerializedState
        {
            [Variant] public static partial SerializedState First(int value);
            [Variant] public static partial SerializedState Second(string text);
        }

        [Serializable, DiscriminatedUnion]
        public partial struct OtherState
        {
            [Variant] public static partial OtherState First(int value);
        }

        [Serializable, DiscriminatedUnion]
        public partial struct GenericState<T> where T : unmanaged
        {
            [Variant] public static partial GenericState<T> First(T value);
        }

        public static partial class Host
        {
            [Serializable, DiscriminatedUnion]
            public partial struct NestedState
            {
                [Variant] public static partial NestedState First(int value);
            }
        }

        [DiscriminatedUnion]
        public partial struct PlainState
        {
            [Variant] public static partial PlainState First(int value);
            [Variant] public static partial PlainState Second(string text);
        }
        """;

    private static string AliasSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct AliasState
        {
            [Variant(FormerlySerializedAs = "Old")]
            public static partial AliasState Current(int value);

            [Variant]
            public static partial AliasState Idle();
        }
        """;

    private static string TupleStateSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct TupleState
        {
            [Variant] public static partial TupleState First(int value);
            [Variant] public static partial TupleState Second(string text);
            [Variant] public static partial TupleState Node([UnitySerializeReference] TupleNode? node);
        }

        [Serializable]
        public sealed class TupleNode { }
        """;
}
