using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnitySerializationBridgeTests
{
    // ── Payload reuse and replacement ─────────────────────────────────

    public static IEnumerable<object[]> LifecycleProfiles =>
        UnityPayloadTestSupport.LayoutUnionNames
            .SelectMany(typeName => new[]
            {
                new object[] { typeName, true },
                new object[] { typeName, false },
            });

    [Fact]
    public void Bridge_ReplacesChangedPayloadWithoutMutatingPriorSnapshot()
    {
        var result = UnityPayloadTestSupport.Run(includeEditorReferences: true);
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge snapshot replacement and reuse");
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var parentType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedParentUnion");

        var priorChild = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 20, "prior");
        var prior = UnityPayloadTestSupport.InvokeFactory(parentType, "Child", priorChild, 100);
        UnityPayloadTestSupport.InvokeCallback(prior, "OnBeforeSerialize");
        var priorHydratedChild = UnityPayloadTestSupport.GetPropertyValue(prior, "child")!;
        var priorSerializedPayload = UnityPayloadTestSupport.GetPayload(prior)!;
        var priorPayloadChild = UnityPayloadTestSupport.GetFieldValue(
            priorSerializedPayload,
            "child")!;
        var priorPayload = UnityPayloadTestSupport.GetPayload(priorPayloadChild);
        var sourceChild = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 21, "current");
        var source = UnityPayloadTestSupport.InvokeFactory(parentType, "Child", sourceChild, 101);
        var sourceNestedChild = UnityPayloadTestSupport.GetPropertyValue(source, "child")!;

        Assert.NotNull(priorPayload);
        Assert.Null(UnityPayloadTestSupport.GetPayload(sourceNestedChild));

        var prepared = UnitySerializationBridge.Prepare(source, prior);
        var preparedChild = UnityPayloadTestSupport.GetPropertyValue(prepared!, "child")!;
        var preparedSerializedPayload = UnityPayloadTestSupport.GetPayload(prepared!)!;
        var preparedPayloadChild = UnityPayloadTestSupport.GetFieldValue(
            preparedSerializedPayload,
            "child")!;

        Assert.NotNull(prepared);
        Assert.NotSame(priorPayload, UnityPayloadTestSupport.GetPayload(preparedPayloadChild));
        Assert.Equal(101, UnityPayloadTestSupport.GetPropertyValue(prepared!, "marker"));
        Assert.Equal(21, UnityPayloadTestSupport.GetPropertyValue(preparedChild, "count"));
        Assert.Equal("current", UnityPayloadTestSupport.GetPropertyValue(preparedChild, "label"));
        Assert.Equal(20, UnityPayloadTestSupport.GetFieldValue(priorPayload!, "count"));
        Assert.Equal("prior", UnityPayloadTestSupport.GetFieldValue(priorPayload!, "label"));

        var unchangedSource = UnityPayloadTestSupport.InvokeFactory(
            parentType,
            "Child",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 20, "prior"),
            100);
        var reused = UnitySerializationBridge.Prepare(unchangedSource, prior);
        var reusedChild = UnityPayloadTestSupport.GetPropertyValue(reused!, "child")!;
        var reusedSerializedPayload = UnityPayloadTestSupport.GetPayload(reused!)!;
        var reusedPayloadChild = UnityPayloadTestSupport.GetFieldValue(
            reusedSerializedPayload,
            "child")!;

        Assert.Same(priorPayload, UnityPayloadTestSupport.GetPayload(reusedPayloadChild));
        Assert.Equal(20, UnityPayloadTestSupport.GetPropertyValue(reusedChild, "count"));
        Assert.Equal("prior", UnityPayloadTestSupport.GetPropertyValue(reusedChild, "label"));

        var sourceWithCacheChild = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 30, "after");
        UnityPayloadTestSupport.InvokeCallback(sourceWithCacheChild, "OnBeforeSerialize");
        var sourceWithCachePayload = UnityPayloadTestSupport.GetPayload(sourceWithCacheChild);
        var sourceWithCache = UnityPayloadTestSupport.InvokeFactory(
            parentType,
            "Child",
            sourceWithCacheChild,
            102);
        var sourceWithCacheNestedChild = UnityPayloadTestSupport.GetPropertyValue(
            sourceWithCache,
            "child")!;
        var sourcePayload = UnityPayloadTestSupport.GetPayload(sourceWithCacheNestedChild);
        Assert.NotNull(sourcePayload);
        Assert.Same(sourceWithCachePayload, sourcePayload);
        UnityPayloadTestSupport.InvokeCallback(sourceWithCacheNestedChild, "OnAfterDeserialize");
        UnityPayloadTestSupport.SetPropertyValue(
            sourceWithCache,
            "child",
            sourceWithCacheNestedChild);
        Assert.Same(sourcePayload, UnityPayloadTestSupport.GetPayload(sourceWithCacheNestedChild));

        var preparedAfterDeserialize = UnitySerializationBridge.Prepare(
            sourceWithCache,
            prior);
        var preparedAfterDeserializeChild = UnityPayloadTestSupport.GetPropertyValue(
            preparedAfterDeserialize!,
            "child")!;
        var preparedAfterDeserializePayload = UnityPayloadTestSupport.GetPayload(
            preparedAfterDeserialize!)!;
        var preparedAfterDeserializePayloadChild = UnityPayloadTestSupport.GetFieldValue(
            preparedAfterDeserializePayload,
            "child")!;

        Assert.NotSame(
            priorPayload,
            UnityPayloadTestSupport.GetPayload(preparedAfterDeserializePayloadChild));
        Assert.Equal(30, UnityPayloadTestSupport.GetFieldValue(
            UnityPayloadTestSupport.GetPayload(preparedAfterDeserializePayloadChild)!,
            "count"));
        Assert.Equal("after", UnityPayloadTestSupport.GetFieldValue(
            UnityPayloadTestSupport.GetPayload(preparedAfterDeserializePayloadChild)!,
            "label"));
        Assert.Equal(20, UnityPayloadTestSupport.GetFieldValue(priorPayload!, "count"));
        Assert.Equal("prior", UnityPayloadTestSupport.GetFieldValue(priorPayload!, "label"));
    }

    [Fact]
    public void Bridge_SnapshotComparisonUsesSerializedFieldsAndScalarMarkers()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "serialized-field snapshot comparison");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityEqualsProbeUnion");
        var valueType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityEqualsAlways");

        var priorValue = Activator.CreateInstance(valueType)!;
        UnityPayloadTestSupport.SetField(priorValue, "serializedValue", 10);
        var prior = UnityPayloadTestSupport.InvokeFactory(unionType, "Value", priorValue, 1);
        UnityPayloadTestSupport.InvokeCallback(prior, "OnBeforeSerialize");
        var priorPayload = UnityPayloadTestSupport.GetPayload(prior)!;

        var changedValue = Activator.CreateInstance(valueType)!;
        UnityPayloadTestSupport.SetField(changedValue, "serializedValue", 20);
        var changed = UnityPayloadTestSupport.InvokeFactory(unionType, "Value", changedValue, 2);
        var prepared = UnitySerializationBridge.Prepare(changed, prior)!;
        var preparedPayload = UnityPayloadTestSupport.GetPayload(prepared)!;

        Assert.NotSame(priorPayload, preparedPayload);
        Assert.Equal(10, UnityPayloadTestSupport.GetFieldValue(
            UnityPayloadTestSupport.GetFieldValue(priorPayload, "value")!,
            "serializedValue"));
        Assert.Equal(20, UnityPayloadTestSupport.GetFieldValue(
            UnityPayloadTestSupport.GetFieldValue(preparedPayload, "value")!,
            "serializedValue"));
        Assert.Equal(1, UnityPayloadTestSupport.GetFieldValue(priorPayload, "marker"));
        Assert.Equal(2, UnityPayloadTestSupport.GetFieldValue(preparedPayload, "marker"));
    }

    [Fact]
    public void Bridge_RepeatedExplicitHydration_PreservesValues()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "repeated bridge hydration");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var parentType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedParentUnion");
        var child = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 8, "nested");
        var parent = UnityPayloadTestSupport.InvokeFactory(parentType, "Child", child, 41);
        UnityPayloadTestSupport.InvokeCallback(parent, "OnBeforeSerialize");

        var prepared = UnitySerializationBridge.Prepare(parent);
        var preparedPayload = UnityPayloadTestSupport.GetPayload(prepared!)!;
        var preparedChildPayload = UnityPayloadTestSupport.GetPayload(
            UnityPayloadTestSupport.GetFieldValue(preparedPayload, "child")!);
        var first = UnitySerializationBridge.Restore(prepared);
        var firstChild = UnityPayloadTestSupport.GetPropertyValue(first!, "child")!;
        var second = UnitySerializationBridge.Restore(first);
        var secondChild = UnityPayloadTestSupport.GetPropertyValue(second!, "child")!;
        var firstPayload = UnityPayloadTestSupport.GetPayload(first!);
        var secondPayload = UnityPayloadTestSupport.GetPayload(second!);

        Assert.Equal(41, UnityPayloadTestSupport.GetPropertyValue(first!, "marker"));
        Assert.Equal(8, UnityPayloadTestSupport.GetPropertyValue(firstChild, "count"));
        Assert.Equal("nested", UnityPayloadTestSupport.GetPropertyValue(firstChild, "label"));
        Assert.Equal(41, UnityPayloadTestSupport.GetPropertyValue(second!, "marker"));
        Assert.Equal(8, UnityPayloadTestSupport.GetPropertyValue(secondChild, "count"));
        Assert.Equal("nested", UnityPayloadTestSupport.GetPropertyValue(secondChild, "label"));
        Assert.Equal(8, UnityPayloadTestSupport.GetFieldValue(preparedChildPayload!, "count"));
        Assert.Equal("nested", UnityPayloadTestSupport.GetFieldValue(preparedChildPayload!, "label"));
        Assert.NotSame(preparedChildPayload, firstPayload);
        Assert.Same(firstPayload, secondPayload);
    }

    [Theory]
    [MemberData(nameof(LifecycleProfiles))]
    public void Lifecycle_OnAfterDeserialize_ValidPayloadResetsLayout(
        string typeName,
        bool includeEditorReferences)
    {
        var result = UnityPayloadTestSupport.Run(includeEditorReferences);
        var assembly = UnityPayloadTestSupport.Emit(
            result,
            includeEditorReferences
                ? "Editor valid payload callback"
                : "player valid payload callback");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var payloadType = UnityPayloadTestSupport.FindCanonicalPayloadType(
            unionType,
            new[] { "amount", "label" });
        var payload = Activator.CreateInstance(payloadType, nonPublic: true)!;
        UnityPayloadTestSupport.SetField(payload, "amount", 7);
        UnityPayloadTestSupport.SetField(payload, "label", "serialized");

        var stale = UnityPayloadTestSupport.InvokeFactory(
            unionType,
            "Nested",
            UnityPayloadTestSupport.InvokeFactory(
                childType,
                "Text",
                99,
                "stale"));
        UnityPayloadTestSupport.SetField(
            stale,
            "_kind",
            Enum.ToObject(
                UnityPayloadTestSupport.GetField(unionType, "_kind").FieldType,
                1));
        UnityPayloadTestSupport.SetField(stale, "_serializedPayload", payload);

        UnityPayloadTestSupport.InvokeCallback(stale, "OnAfterDeserialize");
        var child = UnityPayloadTestSupport.GetPropertyValue(stale, "child")!;

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(stale));
        Assert.Equal(7, UnityPayloadTestSupport.GetPropertyValue(stale, "amount"));
        Assert.Equal("serialized", UnityPayloadTestSupport.GetPropertyValue(stale, "label"));
        Assert.Equal("Text", UnityPayloadTestSupport.GetKindName(child));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(child, "count"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(child, "label"));
        if (includeEditorReferences)
        {
            Assert.Same(payload, UnityPayloadTestSupport.GetPayload(stale));
            var prepared = UnitySerializationBridge.Prepare(stale)!;
            Assert.Same(payload, UnityPayloadTestSupport.GetPayload(prepared));
        }
        else
        {
            var marker = UnityPayloadTestSupport.GetPayload(stale);
            Assert.NotNull(marker);
            Assert.DoesNotContain(
                UnityPayloadTestSupport.ConcretePayloadTypes(unionType),
                payloadType => payloadType.IsInstanceOfType(marker));
        }
    }

    [Fact]
    public void BoxedTuple_PairRepeatedSerialize_PreservesTupleFields()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "BoxedTuple pair repeated serialization");
        var pairType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityBoxedTuplePairUnion");
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var pair = UnityPayloadTestSupport.InvokeFactory(
            pairType,
            "Pair",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 1, "before"),
            7,
            "pair");

        UnityPayloadTestSupport.InvokeCallback(pair, "OnBeforeSerialize");
        var firstPayload = UnityPayloadTestSupport.GetPayload(pair);
        UnityPayloadTestSupport.InvokeCallback(pair, "OnBeforeSerialize");
        var repeatedPayload = UnityPayloadTestSupport.GetPayload(pair);
        var firstChild = UnityPayloadTestSupport.GetPropertyValue(pair, "child")!;

        Assert.NotNull(firstPayload);
        Assert.Same(firstPayload, repeatedPayload);
        Assert.Equal(1, UnityPayloadTestSupport.GetPropertyValue(firstChild, "count"));
        Assert.Equal("before", UnityPayloadTestSupport.GetPropertyValue(firstChild, "label"));
        Assert.Equal(7, UnityPayloadTestSupport.GetPropertyValue(pair, "marker"));
        Assert.Equal("pair", UnityPayloadTestSupport.GetPropertyValue(pair, "label"));

        var updatedChild = UnityPayloadTestSupport.InvokeFactory(
            childType,
            "Text",
            2,
            "after");
        UnityPayloadTestSupport.SetPropertyValue(pair, "child", updatedChild);
        UnityPayloadTestSupport.InvokeCallback(pair, "OnBeforeSerialize");
        UnityPayloadTestSupport.InvokeCallback(pair, "OnBeforeSerialize");
        var updatedPayload = UnityPayloadTestSupport.GetPayload(pair)!;
        var updatedPayloadChild = UnityPayloadTestSupport.GetFieldValue(updatedPayload, "child")!;

        Assert.Equal(2, UnityPayloadTestSupport.GetPropertyValue(updatedPayloadChild, "count"));
        Assert.Equal("after", UnityPayloadTestSupport.GetPropertyValue(updatedPayloadChild, "label"));
        Assert.Equal(7, UnityPayloadTestSupport.GetFieldValue(updatedPayload, "marker"));
        Assert.Equal("pair", UnityPayloadTestSupport.GetFieldValue(updatedPayload, "label"));
    }

    [Fact]
    public void BoxedTuple_PairValidDeserialize_ResetsInactiveFields()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "BoxedTuple pair valid deserialization");
        var pairType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityBoxedTuplePairUnion");
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var serializedPair = UnityPayloadTestSupport.InvokeFactory(
            pairType,
            "Pair",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 3, "serialized"),
            11,
            "valid");
        UnityPayloadTestSupport.InvokeCallback(serializedPair, "OnBeforeSerialize");
        var payload = UnityPayloadTestSupport.GetPayload(serializedPair);
        var stale = UnityPayloadTestSupport.InvokeFactory(pairType, "Other", 99f);
        UnityPayloadTestSupport.SetField(
            stale,
            "_kind",
            Enum.ToObject(
                UnityPayloadTestSupport.GetField(pairType, "_kind").FieldType,
                1));
        UnityPayloadTestSupport.SetField(stale, "_serializedPayload", payload);

        UnityPayloadTestSupport.InvokeCallback(stale, "OnAfterDeserialize");
        var child = UnityPayloadTestSupport.GetPropertyValue(stale, "child")!;

        Assert.Equal("Pair", UnityPayloadTestSupport.GetKindName(stale));
        Assert.Equal(3, UnityPayloadTestSupport.GetPropertyValue(child, "count"));
        Assert.Equal("serialized", UnityPayloadTestSupport.GetPropertyValue(child, "label"));
        Assert.Equal(11, UnityPayloadTestSupport.GetPropertyValue(stale, "marker"));
        Assert.Equal("valid", UnityPayloadTestSupport.GetPropertyValue(stale, "label"));
        Assert.Equal(0f, UnityPayloadTestSupport.GetPropertyValue(stale, "value"));
    }

    [Fact]
    public void BoxedTupleGeneric_NestedUnionValuePrepareAndSerialize()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "generic BoxedTuple nested union");
        var genericType = UnityPayloadTestSupport.RequireType(
                assembly,
                "StabilityBoxedTupleGenericUnion`1")
            .MakeGenericType(UnityPayloadTestSupport.RequireType(
                assembly,
                "StabilityNestedChildUnion"));
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var value = UnityPayloadTestSupport.InvokeFactory(
            genericType,
            "Value",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 4, "generic"));

        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var firstPayload = UnityPayloadTestSupport.GetPayload(value);
        var runtimeChild = UnityPayloadTestSupport.GetPropertyValue(value, "value")!;
        var payloadChild = UnityPayloadTestSupport.GetFieldValue(firstPayload!, "value")!;

        Assert.NotNull(firstPayload);
        Assert.Equal(4, UnityPayloadTestSupport.GetPropertyValue(runtimeChild, "count"));
        Assert.Equal("generic", UnityPayloadTestSupport.GetPropertyValue(runtimeChild, "label"));
        Assert.Equal(4, UnityPayloadTestSupport.GetPropertyValue(payloadChild, "count"));
        Assert.Equal("generic", UnityPayloadTestSupport.GetPropertyValue(payloadChild, "label"));

        var prepared = UnitySerializationBridge.Prepare(value)!;
        var preparedAgain = UnitySerializationBridge.Prepare(prepared)!;
        var preparedChild = UnityPayloadTestSupport.GetPropertyValue(preparedAgain, "value")!;

        Assert.Same(
            UnityPayloadTestSupport.GetPayload(prepared),
            UnityPayloadTestSupport.GetPayload(preparedAgain));
        Assert.Equal(4, UnityPayloadTestSupport.GetPropertyValue(preparedChild, "count"));
        Assert.Equal("generic", UnityPayloadTestSupport.GetPropertyValue(preparedChild, "label"));
    }

    [Fact]
    public void BoxedTupleReference_OnBeforeSerializePreservesHelperIdentity()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "BoxedTuple reference identity");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityBoxedTupleReferenceUnion");
        var helperType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityBoxedTupleReferenceHelper");
        var helper = Activator.CreateInstance(helperType)!;
        UnityPayloadTestSupport.SetField(helper, "marker", 42);
        var value = UnityPayloadTestSupport.InvokeFactory(unionType, "Reference", helper, 9);

        Assert.Same(helper, UnityPayloadTestSupport.GetPropertyValue(value, "helper"));
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var firstPayload = UnityPayloadTestSupport.GetPayload(value)!;
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var secondPayload = UnityPayloadTestSupport.GetPayload(value)!;

        Assert.Same(helper, UnityPayloadTestSupport.GetPropertyValue(value, "helper"));
        Assert.Same(helper, UnityPayloadTestSupport.GetFieldValue(firstPayload, "helper"));
        Assert.Same(helper, UnityPayloadTestSupport.GetFieldValue(secondPayload, "helper"));
        Assert.Equal(42, UnityPayloadTestSupport.GetFieldValue(helper, "marker"));
        Assert.Equal(9, UnityPayloadTestSupport.GetPropertyValue(value, "marker"));
    }

    [Fact]
    public void Bridge_ExplicitHydration_IsNotNativeDeserialize()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "explicit hydration contract");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var value = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 12, "explicit");
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var payload = UnityPayloadTestSupport.GetPayload(value);

        var explicitFirst = UnitySerializationBridge.Restore(value)!;
        var pureFallback = UnityPayloadTestSupport.InvokeFromSerialized(childType, null);
        var explicitSecond = UnityPayloadTestSupport.InvokeFromSerialized(childType, payload);

        Assert.Equal(12, UnityPayloadTestSupport.GetPropertyValue(explicitFirst, "count"));
        Assert.Equal("explicit", UnityPayloadTestSupport.GetPropertyValue(explicitFirst, "label"));
        Assert.Equal("Text", UnityPayloadTestSupport.GetKindName(pureFallback));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(pureFallback, "count"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(pureFallback, "label"));
        Assert.Equal(12, UnityPayloadTestSupport.GetPropertyValue(explicitSecond, "count"));
        Assert.Equal("explicit", UnityPayloadTestSupport.GetPropertyValue(explicitSecond, "label"));
    }

    [Fact]
    public void Bridge_InitializerChildWithNullPayload_UsesFirstDefaults()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "initializer child fallback");
        var hostType = UnityPayloadTestSupport.RequireType(assembly, "StabilityInitializerHost");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var host = Activator.CreateInstance(hostType)!;
        var initializedChild = UnityPayloadTestSupport.GetFieldValue(host, "child")!;

        Assert.Equal(childType, initializedChild.GetType());
        Assert.Equal(99, UnityPayloadTestSupport.GetPropertyValue(initializedChild, "count"));
        Assert.Equal("initializer", UnityPayloadTestSupport.GetPropertyValue(initializedChild, "label"));
        Assert.Null(UnityPayloadTestSupport.GetPayload(initializedChild));

        var hydrated = UnitySerializationBridge.Restore(host)!;
        var fallbackChild = UnityPayloadTestSupport.GetFieldValue(hydrated, "child")!;

        Assert.Equal("Text", UnityPayloadTestSupport.GetKindName(fallbackChild));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(fallbackChild, "count"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(fallbackChild, "label"));

        var validHost = Activator.CreateInstance(hostType)!;
        var validChild = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 99, "serialized");
        UnityPayloadTestSupport.InvokeCallback(validChild, "OnBeforeSerialize");
        Assert.NotNull(UnityPayloadTestSupport.GetPayload(validChild));
        UnityPayloadTestSupport.SetField(validHost, "child", validChild);
        var validHydrated = UnitySerializationBridge.Restore(validHost)!;
        var validRestoredChild = UnityPayloadTestSupport.GetFieldValue(validHydrated, "child")!;

        Assert.Equal(99, UnityPayloadTestSupport.GetPropertyValue(validRestoredChild, "count"));
        Assert.Equal("serialized", UnityPayloadTestSupport.GetPropertyValue(validRestoredChild, "label"));
    }

    [Fact]
    public void Bridge_ReconstructedChildWithClearedPayload_PreservesRuntimeValues()
    {
        var result = UnityPayloadTestSupport.Run(includeEditorReferences: false);
        var assembly = UnityPayloadTestSupport.Emit(result, "reconstructed child cache cleanup");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var serializedValue = UnityPayloadTestSupport.InvokeFactory(childType, "Text", 55, "reconstructed");
        UnityPayloadTestSupport.InvokeCallback(serializedValue, "OnBeforeSerialize");
        var payload = UnityPayloadTestSupport.GetPayload(serializedValue);
        var reconstructed = UnityPayloadTestSupport.InvokeFromSerialized(childType, payload);

        Assert.Equal(55, UnityPayloadTestSupport.GetPropertyValue(reconstructed, "count"));
        Assert.Equal("reconstructed", UnityPayloadTestSupport.GetPropertyValue(reconstructed, "label"));
        UnityPayloadTestSupport.InvokeCallback(reconstructed, "OnAfterDeserialize");
        var marker = UnityPayloadTestSupport.GetPayload(reconstructed);
        Assert.NotNull(marker);
        Assert.DoesNotContain(
            UnityPayloadTestSupport.ConcretePayloadTypes(childType),
            payloadType => payloadType.IsInstanceOfType(marker));

        var reconstructedAgain = UnityPayloadTestSupport.InvokeFromSerialized(childType, payload);
        UnityPayloadTestSupport.InvokeCallback(reconstructedAgain, "OnAfterDeserialize");
        Assert.Same(marker, UnityPayloadTestSupport.GetPayload(reconstructedAgain));

        var hydrated = UnitySerializationBridge.Restore(reconstructed)!;
        var hydratedAgain = UnitySerializationBridge.Restore(hydrated)!;

        Assert.Equal(55, UnityPayloadTestSupport.GetPropertyValue(hydrated, "count"));
        Assert.Equal("reconstructed", UnityPayloadTestSupport.GetPropertyValue(hydrated, "label"));
        Assert.Equal(55, UnityPayloadTestSupport.GetPropertyValue(hydratedAgain, "count"));
        Assert.Equal("reconstructed", UnityPayloadTestSupport.GetPropertyValue(hydratedAgain, "label"));
    }

    [Fact]
    public void PlayerCleanup_UsesSharedMarkerAndReleasesPayloadGraph()
    {
        var result = UnityPayloadTestSupport.Run(includeEditorReferences: false);
        var assembly = UnityPayloadTestSupport.Emit(result, "player marker and payload release");
        var childType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityNestedChildUnion");
        var probe = CreatePlayerCleanupProbe(childType);

        Assert.Same(probe.Marker, UnityPayloadTestSupport.GetPayload(probe.Second));
        Assert.Equal(55, UnityPayloadTestSupport.GetPropertyValue(probe.First, "count"));
        Assert.Equal("reconstructed", UnityPayloadTestSupport.GetPropertyValue(probe.First, "label"));

        UnityPayloadTestSupport.InvokeCallback(probe.First, "OnBeforeSerialize");
        var preparedPayload = UnityPayloadTestSupport.GetPayload(probe.First);
        Assert.NotSame(probe.Marker, preparedPayload);
        Assert.Contains(
            UnityPayloadTestSupport.ConcretePayloadTypes(childType),
            payloadType => payloadType.IsInstanceOfType(preparedPayload));

        var hydrated = UnitySerializationBridge.Restore(probe.First)!;
        var hydratedAgain = UnitySerializationBridge.Restore(hydrated)!;
        Assert.Equal(55, UnityPayloadTestSupport.GetPropertyValue(hydratedAgain, "count"));
        Assert.Equal("reconstructed", UnityPayloadTestSupport.GetPropertyValue(hydratedAgain, "label"));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(probe.Payload.IsAlive);
    }

    private static (WeakReference Payload, object Marker, object First, object Second)
        CreatePlayerCleanupProbe(Type childType)
    {
        var serializedValue = UnityPayloadTestSupport.InvokeFactory(
            childType,
            "Text",
            55,
            "reconstructed");
        UnityPayloadTestSupport.InvokeCallback(serializedValue, "OnBeforeSerialize");
        object? payload = UnityPayloadTestSupport.GetPayload(serializedValue);
        Assert.NotNull(payload);
        var first = UnityPayloadTestSupport.InvokeFromSerialized(childType, payload);
        UnityPayloadTestSupport.InvokeCallback(first, "OnAfterDeserialize");
        var marker = UnityPayloadTestSupport.GetPayload(first)!;
        var second = UnityPayloadTestSupport.InvokeFromSerialized(childType, payload);
        UnityPayloadTestSupport.InvokeCallback(second, "OnAfterDeserialize");
        var payloadReference = new WeakReference(payload!);
        payload = null;
        serializedValue = null!;
        return (payloadReference, marker, first, second);
    }

    // ── Callback order, copies, and sibling collection isolation ──────

    [Fact]
    public void Bridge_CallbackOrdering_IsIndependent()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge callback ordering");
        var hostType = UnityPayloadTestSupport.RequireType(assembly, "StabilityBridgeHost");
        var parentType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedParentUnion");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");

        var hostParentFirst = CreateBridgeHost(hostType, parentType, childType, 5);
        var hostChildFirst = CreateBridgeHost(hostType, parentType, childType, 5);
        var parentA = UnityPayloadTestSupport.GetFieldValue(hostParentFirst, "parent")!;
        var childA = UnityPayloadTestSupport.GetPropertyValue(parentA, "child")!;
        UnityPayloadTestSupport.InvokeCallback(parentA, "OnBeforeSerialize");
        UnityPayloadTestSupport.InvokeCallback(childA, "OnBeforeSerialize");
        UnityPayloadTestSupport.SetPropertyValue(parentA, "child", childA);
        UnityPayloadTestSupport.SetField(hostParentFirst, "parent", parentA);

        var parentB = UnityPayloadTestSupport.GetFieldValue(hostChildFirst, "parent")!;
        var childB = UnityPayloadTestSupport.GetPropertyValue(parentB, "child")!;
        UnityPayloadTestSupport.InvokeCallback(childB, "OnBeforeSerialize");
        UnityPayloadTestSupport.InvokeCallback(parentB, "OnBeforeSerialize");
        UnityPayloadTestSupport.SetPropertyValue(parentB, "child", childB);
        UnityPayloadTestSupport.SetField(hostChildFirst, "parent", parentB);

        var restoredA = UnitySerializationBridge.Restore(UnitySerializationBridge.Prepare(hostParentFirst))!;
        var restoredB = UnitySerializationBridge.Restore(UnitySerializationBridge.Prepare(hostChildFirst))!;
        var restoredParentA = UnityPayloadTestSupport.GetFieldValue(restoredA, "parent")!;
        var restoredParentB = UnityPayloadTestSupport.GetFieldValue(restoredB, "parent")!;
        var restoredChildA = UnityPayloadTestSupport.GetPropertyValue(restoredParentA, "child")!;
        var restoredChildB = UnityPayloadTestSupport.GetPropertyValue(restoredParentB, "child")!;

        Assert.Equal(5, UnityPayloadTestSupport.GetPropertyValue(restoredParentA, "marker"));
        Assert.Equal(5, UnityPayloadTestSupport.GetPropertyValue(restoredParentB, "marker"));
        Assert.Equal(5, UnityPayloadTestSupport.GetPropertyValue(restoredChildA, "count"));
        Assert.Equal(5, UnityPayloadTestSupport.GetPropertyValue(restoredChildB, "count"));
        Assert.Equal("callback-order", UnityPayloadTestSupport.GetPropertyValue(restoredChildA, "label"));
        Assert.Equal("callback-order", UnityPayloadTestSupport.GetPropertyValue(restoredChildB, "label"));
        Assert.Equal(
            restoredChildA.GetType(),
            restoredChildB.GetType());
        Assert.Equal(
            UnityPayloadTestSupport.GetPayload(restoredChildA)!.GetType(),
            UnityPayloadTestSupport.GetPayload(restoredChildB)!.GetType());
    }

    [Fact]
    public void Bridge_CopyIsolation()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge copy isolation");
        var parentType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedParentUnion");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var original = UnityPayloadTestSupport.InvokeFactory(
            parentType,
            "Child",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 3, "original"),
            11);
        var copy = UnityPayloadTestSupport.InvokeFactory(
            parentType,
            "Child",
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", 3, "original"),
            11);
        var originalPrepared = UnitySerializationBridge.Prepare(original)!;
        var copyPrepared = UnitySerializationBridge.Prepare(copy)!;
        var originalChild = UnityPayloadTestSupport.GetPropertyValue(originalPrepared, "child")!;
        var copyChild = UnityPayloadTestSupport.GetPropertyValue(copyPrepared, "child")!;
        var originalChildPayload = UnityPayloadTestSupport.GetPayload(
            UnityPayloadTestSupport.GetFieldValue(
                UnityPayloadTestSupport.GetPayload(originalPrepared)!,
                "child")!);
        var copyChildPayload = UnityPayloadTestSupport.GetPayload(
            UnityPayloadTestSupport.GetFieldValue(
                UnityPayloadTestSupport.GetPayload(copyPrepared)!,
                "child")!);

        Assert.NotSame(
            originalChildPayload,
            copyChildPayload);

        UnityPayloadTestSupport.SetPropertyValue(copyChild, "count", 99);
        UnityPayloadTestSupport.SetPropertyValue(copyChild, "label", "copy");
        UnityPayloadTestSupport.SetPropertyValue(copyPrepared, "child", copyChild);
        UnityPayloadTestSupport.InvokeCallback(copyPrepared, "OnBeforeSerialize");
        var originalRestored = UnitySerializationBridge.Restore(originalPrepared)!;
        var copyRestored = UnitySerializationBridge.Restore(copyPrepared)!;
        var originalRestoredChild = UnityPayloadTestSupport.GetPropertyValue(originalRestored, "child")!;
        var copyRestoredChild = UnityPayloadTestSupport.GetPropertyValue(copyRestored, "child")!;

        Assert.Equal(3, UnityPayloadTestSupport.GetPropertyValue(originalRestoredChild, "count"));
        Assert.Equal("original", UnityPayloadTestSupport.GetPropertyValue(originalRestoredChild, "label"));
        Assert.Equal(99, UnityPayloadTestSupport.GetPropertyValue(copyRestoredChild, "count"));
        Assert.Equal("copy", UnityPayloadTestSupport.GetPropertyValue(copyRestoredChild, "label"));
    }

    [Fact]
    public void Bridge_SiblingArrayIsolation()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge array isolation");
        var hostType = UnityPayloadTestSupport.RequireType(assembly, "StabilityBridgeHost");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var host = Activator.CreateInstance(hostType)!;
        var array = Array.CreateInstance(childType, 2);
        array.SetValue(UnityPayloadTestSupport.InvokeFactory(childType, "Text", 1, "left"), 0);
        array.SetValue(UnityPayloadTestSupport.InvokeFactory(childType, "Text", 2, "right"), 1);
        UnityPayloadTestSupport.SetField(host, "array", array);

        var prepared = UnitySerializationBridge.Prepare(host)!;
        var preparedArray = (Array)UnityPayloadTestSupport.GetFieldValue(prepared, "array")!;
        var first = preparedArray.GetValue(0)!;
        var second = preparedArray.GetValue(1)!;

        Assert.NotSame(
            UnityPayloadTestSupport.GetPayload(first),
            UnityPayloadTestSupport.GetPayload(second));
        UnityPayloadTestSupport.SetPropertyValue(first, "count", 10);
        UnityPayloadTestSupport.SetPropertyValue(first, "label", "changed");
        preparedArray.SetValue(first, 0);

        Assert.Equal(10, UnityPayloadTestSupport.GetPropertyValue(preparedArray.GetValue(0)!, "count"));
        Assert.Equal("changed", UnityPayloadTestSupport.GetPropertyValue(preparedArray.GetValue(0)!, "label"));
        Assert.Equal(2, UnityPayloadTestSupport.GetPropertyValue(preparedArray.GetValue(1)!, "count"));
        Assert.Equal("right", UnityPayloadTestSupport.GetPropertyValue(preparedArray.GetValue(1)!, "label"));
    }

    [Fact]
    public void Bridge_SiblingListIsolation()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge list isolation");
        var hostType = UnityPayloadTestSupport.RequireType(assembly, "StabilityBridgeHost");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var host = Activator.CreateInstance(hostType)!;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType))!;
        list.Add(UnityPayloadTestSupport.InvokeFactory(childType, "Text", 4, "first"));
        list.Add(UnityPayloadTestSupport.InvokeFactory(childType, "Text", 5, "second"));
        UnityPayloadTestSupport.SetField(host, "list", list);

        var prepared = UnitySerializationBridge.Prepare(host)!;
        var preparedList = (IList)UnityPayloadTestSupport.GetFieldValue(prepared, "list")!;
        var first = preparedList[0]!;
        var second = preparedList[1]!;

        Assert.NotSame(
            UnityPayloadTestSupport.GetPayload(first),
            UnityPayloadTestSupport.GetPayload(second));
        UnityPayloadTestSupport.SetPropertyValue(first, "count", 40);
        UnityPayloadTestSupport.SetPropertyValue(first, "label", "changed");
        preparedList[0] = first;

        Assert.Equal(40, UnityPayloadTestSupport.GetPropertyValue(preparedList[0]!, "count"));
        Assert.Equal("changed", UnityPayloadTestSupport.GetPropertyValue(preparedList[0]!, "label"));
        Assert.Equal(5, UnityPayloadTestSupport.GetPropertyValue(preparedList[1]!, "count"));
        Assert.Equal("second", UnityPayloadTestSupport.GetPropertyValue(preparedList[1]!, "label"));
    }

    [Fact]
    public void Bridge_GenericChildIsolation()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "generic bridge isolation");
        var parentType = UnityPayloadTestSupport.RequireType(assembly, "StabilityGenericParent`1")
            .MakeGenericType(typeof(int));
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityGenericUnion`1")
            .MakeGenericType(typeof(int));
        var first = Activator.CreateInstance(parentType)!;
        var second = Activator.CreateInstance(parentType)!;
        UnityPayloadTestSupport.SetField(
            first,
            "value",
            UnityPayloadTestSupport.InvokeFactory(childType, "First", 6));
        UnityPayloadTestSupport.SetField(
            second,
            "value",
            UnityPayloadTestSupport.InvokeFactory(childType, "First", 7));

        var firstPrepared = UnitySerializationBridge.Prepare(first)!;
        var secondPrepared = UnitySerializationBridge.Prepare(second)!;
        var firstChild = UnityPayloadTestSupport.GetFieldValue(firstPrepared, "value")!;
        var secondChild = UnityPayloadTestSupport.GetFieldValue(secondPrepared, "value")!;

        Assert.Equal(childType, firstChild.GetType());
        Assert.Equal(childType, secondChild.GetType());
        Assert.NotSame(
            UnityPayloadTestSupport.GetPayload(firstChild),
            UnityPayloadTestSupport.GetPayload(secondChild));
        Assert.Equal(6, UnityPayloadTestSupport.GetPropertyValue(firstChild, "value"));
        Assert.Equal(7, UnityPayloadTestSupport.GetPropertyValue(secondChild, "value"));
    }

    // ── Fieldless hydration, variant changes, and layout parity ───────

    [Fact]
    public void Bridge_FieldlessChildHydration()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "fieldless bridge hydration");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");
        var parentType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedParentUnion");
        var parent = UnityPayloadTestSupport.InvokeFactory(
            parentType,
            "Child",
            UnityPayloadTestSupport.InvokeFactory(childType, "Empty"),
            77);

        var first = UnitySerializationBridge.Restore(UnitySerializationBridge.Prepare(parent))!;
        var second = UnitySerializationBridge.Restore(first)!;
        var firstChild = UnityPayloadTestSupport.GetPropertyValue(first, "child")!;
        var secondChild = UnityPayloadTestSupport.GetPropertyValue(second, "child")!;
        var secondPayload = UnityPayloadTestSupport.GetPayload(secondChild);

        Assert.Equal(77, UnityPayloadTestSupport.GetPropertyValue(first, "marker"));
        Assert.Equal(77, UnityPayloadTestSupport.GetPropertyValue(second, "marker"));
        Assert.Equal("Empty", UnityPayloadTestSupport.GetKindName(firstChild));
        Assert.Equal("Empty", UnityPayloadTestSupport.GetKindName(secondChild));
        Assert.NotNull(secondPayload);
        Assert.Empty(UnityPayloadTestSupport.DeclaredInstanceFields(secondPayload!.GetType()));
    }

    [Fact]
    public void Bridge_VariantSwitch_DropsPriorSnapshot()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "bridge variant switching");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var original = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Nested",
                UnityPayloadTestSupport.InvokeFactory(childType, "Text", 1, "old"));
            UnityPayloadTestSupport.InvokeCallback(original, "OnBeforeSerialize");
            var originalChild = UnityPayloadTestSupport.GetPropertyValue(original, "child")!;
            var originalPayload = UnityPayloadTestSupport.GetPayload(
                UnityPayloadTestSupport.GetFieldValue(
                    UnityPayloadTestSupport.GetPayload(original)!,
                    "child")!);

            var empty = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Nested",
                UnityPayloadTestSupport.InvokeFactory(childType, "Empty"));
            UnityPayloadTestSupport.InvokeCallback(empty, "OnBeforeSerialize");
            var emptyChild = UnityPayloadTestSupport.GetPropertyValue(empty, "child")!;
            var emptyPayload = UnityPayloadTestSupport.GetPayload(
                UnityPayloadTestSupport.GetFieldValue(
                    UnityPayloadTestSupport.GetPayload(empty)!,
                    "child")!);

            var replacement = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Nested",
                UnityPayloadTestSupport.InvokeFactory(childType, "Text", 2, "new"));
            UnityPayloadTestSupport.InvokeCallback(replacement, "OnBeforeSerialize");
            var replacementChild = UnityPayloadTestSupport.GetPropertyValue(replacement, "child")!;
            var replacementPayload = UnityPayloadTestSupport.GetPayload(
                UnityPayloadTestSupport.GetFieldValue(
                    UnityPayloadTestSupport.GetPayload(replacement)!,
                    "child")!);

            Assert.NotSame(originalPayload, emptyPayload);
            Assert.NotSame(emptyPayload, replacementPayload);
            Assert.Equal("Empty", UnityPayloadTestSupport.GetKindName(emptyChild));
            Assert.Equal("Text", UnityPayloadTestSupport.GetKindName(replacementChild));
            Assert.Equal(1, UnityPayloadTestSupport.GetFieldValue(originalPayload!, "count"));
            Assert.Equal("old", UnityPayloadTestSupport.GetFieldValue(originalPayload!, "label"));
            Assert.Equal(2, UnityPayloadTestSupport.GetFieldValue(replacementPayload!, "count"));
            Assert.Equal("new", UnityPayloadTestSupport.GetFieldValue(replacementPayload!, "label"));
        }
    }

    [Fact]
    public void Bridge_AllFiveLayouts_Parity()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "all layout bridge parity");
        var childType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNestedChildUnion");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var value = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Nested",
                UnityPayloadTestSupport.InvokeFactory(childType, "Text", 13, "parity"));
            UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
            var payload = UnityPayloadTestSupport.GetPayload(value);
            var reconstructed = UnityPayloadTestSupport.InvokeFromSerialized(unionType, payload);
            var child = UnityPayloadTestSupport.GetPropertyValue(reconstructed, "child")!;
            var prepared = UnitySerializationBridge.Prepare(reconstructed)!;
            var restored = UnitySerializationBridge.Restore(prepared)!;
            var restoredChild = UnityPayloadTestSupport.GetPropertyValue(restored, "child")!;

            Assert.Equal("Nested", UnityPayloadTestSupport.GetKindName(reconstructed));
            Assert.Equal(13, UnityPayloadTestSupport.GetPropertyValue(child, "count"));
            Assert.Equal("parity", UnityPayloadTestSupport.GetPropertyValue(child, "label"));
            Assert.Equal(13, UnityPayloadTestSupport.GetPropertyValue(restoredChild, "count"));
            Assert.Equal("parity", UnityPayloadTestSupport.GetPropertyValue(restoredChild, "label"));
        }
    }

    [Fact]
    public void Bridge_CurrentFormatPayload_NotLost()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "current payload preservation");
        var migrationType = UnityPayloadTestSupport.RequireType(assembly, "StabilityMigrationUnion");
        var value = UnityPayloadTestSupport.InvokeFactory(migrationType, "Renamed", 42);
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var currentPayload = UnityPayloadTestSupport.GetPayload(value);
        var first = UnityPayloadTestSupport.InvokeFromSerialized(migrationType, currentPayload);
        UnityPayloadTestSupport.InvokeCallback(first, "OnBeforeSerialize");
        var second = UnityPayloadTestSupport.InvokeFromSerialized(
            migrationType,
            UnityPayloadTestSupport.GetPayload(first));

        Assert.Equal("Renamed", UnityPayloadTestSupport.GetKindName(first));
        Assert.Equal(42, UnityPayloadTestSupport.GetPropertyValue(first, "value"));
        Assert.Equal("Renamed", UnityPayloadTestSupport.GetKindName(second));
        Assert.Equal(42, UnityPayloadTestSupport.GetPropertyValue(second, "value"));
        Assert.Equal(currentPayload!.GetType(), UnityPayloadTestSupport.GetPayload(first)!.GetType());
        Assert.Equal(currentPayload.GetType(), UnityPayloadTestSupport.GetPayload(second)!.GetType());
    }

    private static object CreateBridgeHost(
        Type hostType,
        Type parentType,
        Type childType,
        int count)
    {
        var host = Activator.CreateInstance(hostType)!;
        var child = UnityPayloadTestSupport.InvokeFactory(childType, "Text", count, "callback-order");
        var parent = UnityPayloadTestSupport.InvokeFactory(parentType, "Child", child, count);
        UnityPayloadTestSupport.SetField(host, "parent", parent);
        var array = Array.CreateInstance(childType, 2);
        array.SetValue(
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", count, "array-left"),
            0);
        array.SetValue(
            UnityPayloadTestSupport.InvokeFactory(childType, "Text", count + 1, "array-right"),
            1);
        UnityPayloadTestSupport.SetField(host, "array", array);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType))!;
        list.Add(UnityPayloadTestSupport.InvokeFactory(childType, "Text", count, "list-left"));
        list.Add(UnityPayloadTestSupport.InvokeFactory(childType, "Text", count + 1, "list-right"));
        UnityPayloadTestSupport.SetField(host, "list", list);
        return host;
    }
}
