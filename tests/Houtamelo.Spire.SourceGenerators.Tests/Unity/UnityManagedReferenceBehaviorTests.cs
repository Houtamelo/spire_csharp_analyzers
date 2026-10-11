using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

#if !SPIRE_UNITY_TEST_HOST
using System.Diagnostics;
using System.Threading;
#endif

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityManagedReferenceBehaviorTests
{
    public static IEnumerable<object[]> Layouts =>
        ManagedReferenceTestSupport.LayoutUnionNames
            .Select(typeName => new object[] { typeName });

    // ── Generated metadata and binding ───────────────────────────────

    [Fact]
    public void Metadata_AllFiveLayouts_ManagedAndInlineFields()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference field metadata");

        foreach (var typeName in ManagedReferenceTestSupport.LayoutUnionNames)
        {
            var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
            AssertManagedField(unionType, "Reference", new[] { "value" }, true);
            AssertManagedField(unionType, "Inline", new[] { "value" }, false);
            AssertManagedField(unionType, "Pair", new[] { "first", "second" }, true);
            AssertManagedField(unionType, "Array", new[] { "arrayValues" }, true);
            AssertManagedField(unionType, "List", new[] { "listValues" }, true);
        }

        var renamed = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceRenamedUnion");
        AssertManagedField(renamed, "Reference", new[] { "value" }, true);
        var formerName = ManagedReferenceTestSupport.FindPayloadType(
            renamed,
            new[] { "value" },
            "OldReference");
        Assert.True(ManagedReferenceTestSupport.HasAttribute(
            ManagedReferenceTestSupport.GetField(formerName, "value"),
            "UnityEngine.SerializeField"));
        Assert.True(ManagedReferenceTestSupport.HasAttribute(
            ManagedReferenceTestSupport.GetField(formerName, "value"),
            "UnityEngine.SerializeReference"));
    }

    [Fact]
    public void Metadata_AttributeBindingForms()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference attribute binding");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceBindingUnion");

        AssertManagedField(unionType, "Qualified", new[] { "value" }, true);
        AssertManagedField(unionType, "Aliased", new[] { "value" }, true);
        AssertManagedField(unionType, "Normal", new[] { "value" }, false);
        AssertManagedField(unionType, "Foreign", new[] { "value" }, false);
    }

    [Fact]
    public void Metadata_EligibilityBelongsToUnity()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "Unity-owned managed-reference eligibility");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceEligibilityUnion");

        AssertManagedField(unionType, "Sensitive", new[] { "objectValue", "baseValue", "interfaceValue", "map", "matrix", "asset" }, true);
    }

    [Fact]
    public void Metadata_NonUnityOutputUnchanged()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "non-Unity marker control");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceNonUnityUnion");
        var value = ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", (object?)null);

        Assert.Null(ManagedReferenceTestSupport.GetFieldOrNull(unionType, "_serializedPayload"));
        Assert.Null(unionType.GetMethod("OnBeforeSerialize", ManagedReferenceTestSupport.InstanceFlags));
        Assert.Null(unionType.GetMethod("OnAfterDeserialize", ManagedReferenceTestSupport.InstanceFlags));
        Assert.Equal("Reference", ManagedReferenceTestSupport.GetKindName(value));
        Assert.Null(ManagedReferenceTestSupport.GetPropertyValue(value, "value"));
    }

    // ── Factory, identity, collections, and aliases ──────────────────

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_NullManagedReferenceRoundTrip(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "null managed-reference round trip");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var value = ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", (object?)null);

        ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var restored = ManagedReferenceTestSupport.InvokeFromSerialized(
            unionType,
            ManagedReferenceTestSupport.GetPayload(value));

        Assert.Equal("Reference", ManagedReferenceTestSupport.GetKindName(restored));
        Assert.Null(ManagedReferenceTestSupport.GetPropertyValue(restored, "value"));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_DerivedRuntimeTypeRoundTrip(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "derived managed-reference round trip");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var node = Activator.CreateInstance(nodeType)!;
        ManagedReferenceTestSupport.SetField(node, "leaf", 17);
        var value = ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", node);

        ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var restored = ManagedReferenceTestSupport.InvokeFromSerialized(
            unionType,
            ManagedReferenceTestSupport.GetPayload(value));
        var restoredNode = ManagedReferenceTestSupport.GetPropertyValue(restored, "value");

        Assert.Same(node, restoredNode);
        Assert.Equal(nodeType, restoredNode!.GetType());
        Assert.Equal(17, ManagedReferenceTestSupport.GetFieldValue(restoredNode, "leaf"));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_DistinctManagedReferenceReplacementWinsOverCachedPayload(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "distinct managed-reference replacement");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");

        object CreateLeaf()
        {
            var node = Activator.CreateInstance(nodeType)!;
            ManagedReferenceTestSupport.SetField(node, "leaf", 17);
            ManagedReferenceTestSupport.SetField(node, "name", "same-fields");
            return node;
        }

        var firstReference = CreateLeaf();
        var secondReference = CreateLeaf();
        var reference = ManagedReferenceTestSupport.InvokeFactory(
            unionType,
            "Reference",
            firstReference);
        ManagedReferenceTestSupport.InvokeCallback(reference, "OnBeforeSerialize");
        ManagedReferenceTestSupport.SetPropertyValue(reference, "value", secondReference);
        ManagedReferenceTestSupport.InvokeCallback(reference, "OnBeforeSerialize");
        var referencePayload = ManagedReferenceTestSupport.GetPayload(reference)!;
        Assert.Same(secondReference, ManagedReferenceTestSupport.GetFieldValue(referencePayload, "value"));
        Assert.NotSame(firstReference, ManagedReferenceTestSupport.GetFieldValue(referencePayload, "value"));

        var firstArrayReference = CreateLeaf();
        var secondArrayReference = CreateLeaf();
        var nodeArray = Array.CreateInstance(nodeType.BaseType!, 1);
        nodeArray.SetValue(firstArrayReference, 0);
        var array = ManagedReferenceTestSupport.InvokeFactory(unionType, "Array", nodeArray);
        ManagedReferenceTestSupport.InvokeCallback(array, "OnBeforeSerialize");
        var replacementArray = Array.CreateInstance(nodeType.BaseType!, 1);
        replacementArray.SetValue(secondArrayReference, 0);
        ManagedReferenceTestSupport.SetPropertyValue(array, "arrayValues", replacementArray);
        ManagedReferenceTestSupport.InvokeCallback(array, "OnBeforeSerialize");
        var arrayPayload = ManagedReferenceTestSupport.GetPayload(array)!;
        var arrayPayloadValues = (Array)ManagedReferenceTestSupport.GetFieldValue(
            arrayPayload,
            "arrayValues")!;
        Assert.Same(secondArrayReference, arrayPayloadValues.GetValue(0));
        Assert.NotSame(firstArrayReference, arrayPayloadValues.GetValue(0));

        var firstListReference = CreateLeaf();
        var secondListReference = CreateLeaf();
        var listType = typeof(List<>).MakeGenericType(nodeType.BaseType!);
        var firstList = (IList)Activator.CreateInstance(listType)!;
        firstList.Add(firstListReference);
        var list = ManagedReferenceTestSupport.InvokeFactory(unionType, "List", firstList);
        ManagedReferenceTestSupport.InvokeCallback(list, "OnBeforeSerialize");
        var replacementList = (IList)Activator.CreateInstance(listType)!;
        replacementList.Add(secondListReference);
        ManagedReferenceTestSupport.SetPropertyValue(list, "listValues", replacementList);
        ManagedReferenceTestSupport.InvokeCallback(list, "OnBeforeSerialize");
        var listPayload = ManagedReferenceTestSupport.GetPayload(list)!;
        var listPayloadValues = (IList)ManagedReferenceTestSupport.GetFieldValue(
            listPayload,
            "listValues")!;
        Assert.Same(secondListReference, listPayloadValues[0]);
        Assert.NotSame(firstListReference, listPayloadValues[0]);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_PairPreservesSharedIdentity(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference pair identity");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var node = Activator.CreateInstance(nodeType)!;
        var value = ManagedReferenceTestSupport.InvokeFactory(unionType, "Pair", node, node);

        ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var payload = ManagedReferenceTestSupport.GetPayload(value)!;
        var restored = ManagedReferenceTestSupport.InvokeFromSerialized(unionType, payload);

        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(payload, "first"));
        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(payload, "second"));
        Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(restored, "first"));
        Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(restored, "second"));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_CollectionsPreserveElementIdentity(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference collection identity");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var node = Activator.CreateInstance(nodeType)!;
        var nodes = Array.CreateInstance(nodeType.BaseType!, 3);
        nodes.SetValue(null, 0);
        nodes.SetValue(node, 1);
        nodes.SetValue(node, 2);
        var values = ManagedReferenceTestSupport.InvokeFactory(unionType, "Array", nodes);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nodeType.BaseType!))!;
        list.Add((object?)null);
        list.Add(node);
        list.Add(node);
        var listValue = ManagedReferenceTestSupport.InvokeFactory(unionType, "List", list);

        ManagedReferenceTestSupport.InvokeCallback(values, "OnBeforeSerialize");
        ManagedReferenceTestSupport.InvokeCallback(listValue, "OnBeforeSerialize");
        var arrayPayload = ManagedReferenceTestSupport.GetPayload(values)!;
        var listPayload = ManagedReferenceTestSupport.GetPayload(listValue)!;
        var arrayPayloadValues = (Array)ManagedReferenceTestSupport.GetFieldValue(arrayPayload, "arrayValues")!;
        var listPayloadValues = (IList)ManagedReferenceTestSupport.GetFieldValue(listPayload, "listValues")!;

        Assert.Null(arrayPayloadValues.GetValue(0));
        Assert.Same(node, arrayPayloadValues.GetValue(1));
        Assert.Same(node, arrayPayloadValues.GetValue(2));
        Assert.Null(listPayloadValues[0]);
        Assert.Same(node, listPayloadValues[1]);
        Assert.Same(node, listPayloadValues[2]);
        Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(
            ManagedReferenceTestSupport.InvokeFromSerialized(unionType, arrayPayload),
            "arrayValues") is Array restoredArray ? restoredArray.GetValue(1) : null);
        Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(
            ManagedReferenceTestSupport.InvokeFromSerialized(unionType, listPayload),
            "listValues") is IList restoredList ? restoredList[1] : null);
    }

    [Fact]
    public void Factory_NestedHelperPreservesPrivateReferences()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "nested managed-reference helper");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var helperType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceHelper");
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var node = Activator.CreateInstance(nodeType)!;
        ManagedReferenceTestSupport.SetField(node, "leaf", 17);
        var helper = Activator.CreateInstance(helperType)!;
        var child = ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", node);
        ManagedReferenceTestSupport.SetField(helper, "child", child);
        ManagedReferenceTestSupport.SetField(helper, "node", node);
        ManagedReferenceTestSupport.SetField(helper, "opaque", node);
        var value = ManagedReferenceTestSupport.InvokeFactory(unionType, "Nested", helper);

        ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var prepared = UnitySerializationBridge.Prepare(value)!;
        var restored = UnitySerializationBridge.Restore(prepared)!;
        var preparedHelper = ManagedReferenceTestSupport.GetPropertyValue(prepared, "helper")!;
        var restoredHelper = ManagedReferenceTestSupport.GetPropertyValue(restored, "helper")!;

        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(preparedHelper, "node"));
        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(preparedHelper, "opaque"));
        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "node"));
        Assert.Same(node, ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "opaque"));
        Assert.Equal(17, ManagedReferenceTestSupport.GetFieldValue(node, "leaf") ?? 0);
    }

    [Fact]
    public void Factory_PrivateManagedReferencesTraverseDerivedChildUnionGraph()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "private managed-reference derived child-union graph");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var helperType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceHelper");
        var leafType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicLeaf");
        var childUnionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicChildUnion");

        foreach (var fieldName in new[] { "dynamicBase", "dynamicInterface", "dynamicObject" })
        {
            var field = ManagedReferenceTestSupport.GetField(helperType, fieldName);
            Assert.True(ManagedReferenceTestSupport.HasAttribute(
                field,
                "UnityEngine.SerializeReference"));
            Assert.False(ManagedReferenceTestSupport.HasAttribute(
                field,
                "UnityEngine.SerializeField"));
        }

        var child = ManagedReferenceTestSupport.InvokeFactory(
            childUnionType,
            "Value",
            314);
        var leaf = Activator.CreateInstance(leafType)!;
        ManagedReferenceTestSupport.SetField(leaf, "leaf", 27);
        ManagedReferenceTestSupport.SetField(leaf, "child", child);

        var helper = Activator.CreateInstance(helperType)!;
        ManagedReferenceTestSupport.SetField(helper, "dynamicBase", leaf);
        ManagedReferenceTestSupport.SetField(helper, "dynamicInterface", leaf);
        ManagedReferenceTestSupport.SetField(helper, "dynamicObject", leaf);
        var value = ManagedReferenceTestSupport.InvokeFactory(
            unionType,
            "Nested",
            helper);

        ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var prepared = UnitySerializationBridge.Prepare(value)!;
        var preparedHelper = ManagedReferenceTestSupport.GetPropertyValue(
            prepared,
            "helper")!;

        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(preparedHelper, "dynamicBase"));
        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(preparedHelper, "dynamicInterface"));
        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(preparedHelper, "dynamicObject"));
        Assert.Equal(leafType, leaf.GetType());
        Assert.Equal(
            "Value",
            ManagedReferenceTestSupport.GetKindName(
                ManagedReferenceTestSupport.GetFieldValue(leaf, "child")!));
        Assert.Equal(
            314,
            ManagedReferenceTestSupport.GetPropertyValue(
                ManagedReferenceTestSupport.GetFieldValue(leaf, "child")!,
                "childValue"));

        var restored = UnitySerializationBridge.Restore(prepared)!;
        var restoredHelper = ManagedReferenceTestSupport.GetPropertyValue(
            restored,
            "helper")!;
        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "dynamicBase"));
        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "dynamicInterface"));
        Assert.Same(
            leaf,
            ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "dynamicObject"));
        Assert.Equal(
            27,
            ManagedReferenceTestSupport.GetFieldValue(leaf, "leaf"));
        Assert.Equal(
            314,
            ManagedReferenceTestSupport.GetPropertyValue(
                ManagedReferenceTestSupport.GetFieldValue(leaf, "child")!,
                "childValue"));
    }

    [Fact]
    public void Bridge_DoesNotInvokeUserSerializationCallbacks()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "managed-reference callback boundary");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var helperType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceCallbackHelper");
        var childUnionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicChildUnion");
        var helper = Activator.CreateInstance(helperType)!;
        ManagedReferenceTestSupport.SetField(
            helper,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childUnionType, "Value", 37));
        var value = ManagedReferenceTestSupport.InvokeFactory(
            unionType,
            "Callback",
            helper);

        var prepared = UnitySerializationBridge.Prepare(value)!;
        var preparedHelper = ManagedReferenceTestSupport.GetPropertyValue(
            prepared,
            "callback")!;
        var preparedChild = ManagedReferenceTestSupport.GetFieldValue(
            preparedHelper,
            "child")!;
        var restored = UnitySerializationBridge.Restore(prepared)!;
        var restoredHelper = ManagedReferenceTestSupport.GetPropertyValue(
            restored,
            "callback")!;
        var restoredChild = ManagedReferenceTestSupport.GetFieldValue(
            restoredHelper,
            "child")!;

        Assert.Equal(0, ManagedReferenceTestSupport.GetFieldValue(helper, "beforeCount"));
        Assert.Equal(0, ManagedReferenceTestSupport.GetFieldValue(helper, "afterCount"));
        Assert.Equal(0, ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "beforeCount"));
        Assert.Equal(0, ManagedReferenceTestSupport.GetFieldValue(restoredHelper, "afterCount"));
        Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(preparedChild));
        Assert.Equal(37, ManagedReferenceTestSupport.GetPropertyValue(preparedChild, "childValue"));
        Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(restoredChild));
        Assert.Equal(37, ManagedReferenceTestSupport.GetPropertyValue(restoredChild, "childValue"));

        var callbackInterface = helperType.GetInterface(
            "UnityEngine.ISerializationCallbackReceiver");
        Assert.NotNull(callbackInterface);
        Assert.True(callbackInterface!.IsAssignableFrom(helperType));
        callbackInterface.GetMethod("OnBeforeSerialize")!.Invoke(helper, null);
        callbackInterface.GetMethod("OnAfterDeserialize")!.Invoke(helper, null);

        Assert.Equal(1, ManagedReferenceTestSupport.GetFieldValue(helper, "beforeCount"));
        Assert.Equal(1, ManagedReferenceTestSupport.GetFieldValue(helper, "afterCount"));
    }

    [Fact]
    public void Bridge_UnsupportedMultidimensionalManagedReference_IsIgnored()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "unsupported multidimensional managed reference");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceEligibilityUnion");
        var matrix = new int[2, 2];
        matrix[1, 0] = 23;
        var value = ManagedReferenceTestSupport.InvokeFactory(
            unionType,
            "Sensitive",
            null,
            null,
            null,
            null,
            matrix,
            null);

        var callbackException = Record.Exception(() =>
            ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize"));
        Assert.Null(callbackException);

        var payload = ManagedReferenceTestSupport.GetPayload(value)!;
        Assert.Same(matrix, ManagedReferenceTestSupport.GetFieldValue(payload, "matrix"));
        var prepared = UnitySerializationBridge.Prepare(value)!;
        Assert.Same(matrix, ManagedReferenceTestSupport.GetPropertyValue(prepared, "matrix"));
        var restored = UnitySerializationBridge.Restore(prepared)!;

        Assert.Same(matrix, ManagedReferenceTestSupport.GetPropertyValue(restored, "matrix"));
        Assert.Equal(23, matrix[1, 0]);
    }

    [Fact]
    public void Factory_MixedInlineAndManagedAliasesPrepareManagedRoot()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "mixed inline and managed aliases");
        var hostType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceMixedAliasHost");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var leafType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicLeaf");
        var childUnionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicChildUnion");
        var leaf = Activator.CreateInstance(leafType)!;
        ManagedReferenceTestSupport.SetField(leaf, "leaf", 31);
        ManagedReferenceTestSupport.SetField(
            leaf,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childUnionType, "Value", 271));

        var host = Activator.CreateInstance(hostType)!;
        ManagedReferenceTestSupport.SetField(
            host,
            "inline",
            ManagedReferenceTestSupport.InvokeFactory(unionType, "Inline", leaf));
        ManagedReferenceTestSupport.SetField(
            host,
            "managed",
            ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", leaf));

        var prepared = UnitySerializationBridge.Prepare(host)!;
        var preparedManaged = ManagedReferenceTestSupport.GetFieldValue(
            prepared,
            "managed")!;
        var preparedManagedRoot = ManagedReferenceTestSupport.GetPropertyValue(
            preparedManaged,
            "value")!;
        var preparedChild = ManagedReferenceTestSupport.GetFieldValue(
            preparedManagedRoot,
            "child")!;

        Assert.Same(leaf, preparedManagedRoot);
        Assert.Equal(31, ManagedReferenceTestSupport.GetFieldValue(preparedManagedRoot, "leaf"));
        Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(preparedChild));
        Assert.Equal(271, ManagedReferenceTestSupport.GetPropertyValue(preparedChild, "childValue"));
        Assert.NotNull(ManagedReferenceTestSupport.GetPayload(preparedChild));

        var restored = UnitySerializationBridge.Restore(prepared)!;
        var restoredManaged = ManagedReferenceTestSupport.GetFieldValue(restored, "managed")!;
        var restoredManagedRoot = ManagedReferenceTestSupport.GetPropertyValue(
            restoredManaged,
            "value")!;
        var restoredChild = ManagedReferenceTestSupport.GetFieldValue(
            restoredManagedRoot,
            "child")!;

        Assert.Same(leaf, restoredManagedRoot);
        Assert.Equal(31, ManagedReferenceTestSupport.GetFieldValue(restoredManagedRoot, "leaf"));
        Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(restoredChild));
        Assert.Equal(271, ManagedReferenceTestSupport.GetPropertyValue(restoredChild, "childValue"));
    }

    [Fact]
    public void Bridge_SameManagedReferenceRootRefreshesNestedUnionState()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "same managed root nested union refresh");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var leafType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicLeaf");
        var childUnionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicChildUnion");
        var leaf = Activator.CreateInstance(leafType)!;
        ManagedReferenceTestSupport.SetField(leaf, "leaf", 31);
        ManagedReferenceTestSupport.SetField(
            leaf,
            "next",
            leaf);
        ManagedReferenceTestSupport.SetField(
            leaf,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childUnionType, "Value", 7));
        var root = ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", leaf);

        var firstPrepared = UnitySerializationBridge.Prepare(root)!;
        var firstLeaf = ManagedReferenceTestSupport.GetPropertyValue(firstPrepared, "value")!;
        var firstChild = ManagedReferenceTestSupport.GetFieldValue(firstLeaf, "child")!;
        var firstPayload = ManagedReferenceTestSupport.GetPayload(firstChild)!;
        Assert.Equal(7, ManagedReferenceTestSupport.GetPropertyValue(firstChild, "childValue"));
        Assert.Same(firstLeaf, ManagedReferenceTestSupport.GetFieldValue(firstLeaf, "next"));

        ManagedReferenceTestSupport.SetField(
            leaf,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childUnionType, "Value", 23));
        var secondPrepared = UnitySerializationBridge.Prepare(root, firstPrepared)!;
        var secondLeaf = ManagedReferenceTestSupport.GetPropertyValue(secondPrepared, "value")!;
        var secondChild = ManagedReferenceTestSupport.GetFieldValue(secondLeaf, "child")!;
        var secondPayload = ManagedReferenceTestSupport.GetPayload(secondChild)!;

        Assert.Same(leaf, secondLeaf);
        Assert.NotSame(firstPayload, secondPayload);
        Assert.Equal(23, ManagedReferenceTestSupport.GetPropertyValue(secondChild, "childValue"));
        Assert.Same(secondLeaf, ManagedReferenceTestSupport.GetFieldValue(secondLeaf, "next"));
    }

    [Fact]
    public void Bridge_FieldlessVariantChangeDoesNotReusePriorChildPayload()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "fieldless nested variant snapshot identity");
        var hostType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceFieldlessHost");
        var childType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceFieldlessChildUnion");
        var host = Activator.CreateInstance(hostType)!;
        ManagedReferenceTestSupport.SetField(
            host,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childType, "EmptyA"));

        var firstPrepared = UnitySerializationBridge.Prepare(host)!;
        var firstChild = ManagedReferenceTestSupport.GetFieldValue(firstPrepared, "child")!;
        var firstPayload = ManagedReferenceTestSupport.GetPayload(firstChild)!;
        Assert.Equal("EmptyA", ManagedReferenceTestSupport.GetKindName(firstChild));

        ManagedReferenceTestSupport.SetField(
            host,
            "child",
            ManagedReferenceTestSupport.InvokeFactory(childType, "EmptyB"));
        var secondPrepared = UnitySerializationBridge.Prepare(host, firstPrepared)!;
        var secondChild = ManagedReferenceTestSupport.GetFieldValue(secondPrepared, "child")!;
        var secondPayload = ManagedReferenceTestSupport.GetPayload(secondChild)!;

        Assert.Equal("EmptyB", ManagedReferenceTestSupport.GetKindName(secondChild));
        Assert.NotSame(firstPayload, secondPayload);
        Assert.DoesNotContain("EmptyA", secondPayload.GetType().Name, StringComparison.Ordinal);
        Assert.Contains("EmptyB", secondPayload.GetType().Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_TraversalStateClearsAfterNestedPayloadTransformException()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(
            result,
            "nested payload transform cleanup");
        var weakReferences = RunMalformedNestedOperation(assembly);

        for (var attempt = 0; attempt < 3 &&
             (weakReferences.Root.IsAlive || weakReferences.InvalidPayload.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(weakReferences.Root.IsAlive);
        Assert.False(weakReferences.InvalidPayload.IsAlive);
    }

    private static (WeakReference Root, WeakReference InvalidPayload) RunMalformedNestedOperation(
        Assembly assembly)
    {
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var helperType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceHelper");
        var leafType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicLeaf");
        var childUnionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceDynamicChildUnion");

        object? leaf = Activator.CreateInstance(leafType);
        object? validChild = ManagedReferenceTestSupport.InvokeFactory(
            childUnionType,
            "Value",
            19);
        ManagedReferenceTestSupport.SetField(leaf!, "child", validChild);
        object? invalidPayload = new ArrayList
        {
            leaf,
        };

        object? helper = Activator.CreateInstance(helperType);
        ManagedReferenceTestSupport.SetField(helper!, "dynamicBase", leaf);
        object? root = ManagedReferenceTestSupport.InvokeFactory(
            unionType,
            "Nested",
            helper);
        var rootReference = new WeakReference(root);
        var invalidPayloadReference = new WeakReference(invalidPayload);

        var exception = Record.Exception(() => UnitySerializationBridge.Prepare(invalidPayload!));
        Assert.NotNull(exception);

        var correctedChild = ManagedReferenceTestSupport.InvokeFactory(
            childUnionType,
            "Value",
            23);
        ManagedReferenceTestSupport.SetField(leaf!, "child", correctedChild);
        var prepared = UnitySerializationBridge.Prepare(root!)!;
        var restored = UnitySerializationBridge.Restore(prepared)!;
        var restoredHelper = ManagedReferenceTestSupport.GetPropertyValue(
            restored,
            "helper")!;
        var restoredLeaf = ManagedReferenceTestSupport.GetFieldValue(
            restoredHelper,
            "dynamicBase")!;
        var restoredChild = ManagedReferenceTestSupport.GetFieldValue(
            restoredLeaf,
            "child")!;

        Assert.Same(leaf, restoredLeaf);
        Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(restoredChild));
        Assert.Equal(23, ManagedReferenceTestSupport.GetPropertyValue(restoredChild, "childValue"));

        exception = null;
        prepared = null;
        restored = null;
        root = null;
        helper = null;
        invalidPayload = null;
        leaf = null;
        validChild = null;
        correctedChild = null;
        return (rootReference, invalidPayloadReference);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Factory_IndependentUnionSnapshotsShareOnlyUserReferences(string typeName)
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference snapshot isolation");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, typeName);
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var shared = Activator.CreateInstance(nodeType)!;
        var replacement = Activator.CreateInstance(nodeType)!;
        var first = ManagedReferenceTestSupport.InvokeFactory(unionType, "Pair", shared, shared);
        var second = ManagedReferenceTestSupport.InvokeFactory(unionType, "Pair", shared, shared);

        ManagedReferenceTestSupport.InvokeCallback(first, "OnBeforeSerialize");
        ManagedReferenceTestSupport.InvokeCallback(second, "OnBeforeSerialize");
        var firstPayload = ManagedReferenceTestSupport.GetPayload(first)!;
        var secondPayload = ManagedReferenceTestSupport.GetPayload(second)!;
        Assert.NotSame(firstPayload, secondPayload);
        Assert.Same(shared, ManagedReferenceTestSupport.GetFieldValue(firstPayload, "first"));
        Assert.Same(shared, ManagedReferenceTestSupport.GetFieldValue(secondPayload, "first"));

        ManagedReferenceTestSupport.SetPropertyValue(first, "first", replacement);
        ManagedReferenceTestSupport.InvokeCallback(first, "OnBeforeSerialize");

        Assert.Same(replacement, ManagedReferenceTestSupport.GetFieldValue(
            ManagedReferenceTestSupport.GetPayload(first)!,
            "first"));
        Assert.Same(shared, ManagedReferenceTestSupport.GetFieldValue(
            ManagedReferenceTestSupport.GetPayload(second)!,
            "first"));
        Assert.Same(shared, ManagedReferenceTestSupport.GetPropertyValue(second, "second"));
    }

    [Fact]
    public void FormerlySerializedAs_FormerNameManagedFieldRestores()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "former-name managed-reference payload");
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceRenamedUnion");
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var node = Activator.CreateInstance(nodeType)!;
        var formerNameType = ManagedReferenceTestSupport.FindPayloadType(
            unionType,
            new[] { "value" },
            "OldReference");
        var formerName = Activator.CreateInstance(formerNameType, nonPublic: true)!;
        ManagedReferenceTestSupport.SetField(formerName, "value", node);

        var restored = ManagedReferenceTestSupport.InvokeFromSerialized(unionType, formerName);
        ManagedReferenceTestSupport.InvokeCallback(restored, "OnBeforeSerialize");
        var canonicalPayload = ManagedReferenceTestSupport.GetPayload(restored)!;

        Assert.Equal("Reference", ManagedReferenceTestSupport.GetKindName(restored));
        Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(restored, "value"));
        Assert.DoesNotContain("OldReference", canonicalPayload.GetType().Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_ClosedBaseAndInterfaceManagedReferencesRoundTrip()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "generic managed-reference fields");
        var openType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceGeneric`1");
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "NodeLeaf");
        var interfaceType = ManagedReferenceTestSupport.RequireType(assembly, "INode");
        var node = Activator.CreateInstance(nodeType)!;

        foreach (var closedType in new[] { openType.MakeGenericType(nodeType.BaseType!), openType.MakeGenericType(interfaceType) })
        {
            var value = ManagedReferenceTestSupport.InvokeFactory(closedType, "Value", node);
            ManagedReferenceTestSupport.InvokeCallback(value, "OnBeforeSerialize");
            var restored = ManagedReferenceTestSupport.InvokeFromSerialized(
                closedType,
                ManagedReferenceTestSupport.GetPayload(value));

            Assert.Same(node, ManagedReferenceTestSupport.GetPropertyValue(restored, "value"));
            Assert.Equal("Value", ManagedReferenceTestSupport.GetKindName(restored));
        }
    }

    // ── Cycle probe ──────────────────────────────────────────────────

    [Fact]
    public async Task Factory_CyclicGraphDoesNotCloneOrRecurseForever()
    {
#if SPIRE_UNITY_TEST_HOST
        var unityResult = ManagedReferenceTestSupport.Run();
        var unityAssembly = ManagedReferenceTestSupport.Emit(
            unityResult,
            "Unity-hosted managed-reference cycle metadata");
        AssertManagedReferenceCycleMetadata(unityAssembly);
        RunCycleProbe();
        await Task.CompletedTask;
        return;
#else
        if (string.Equals(
                Environment.GetEnvironmentVariable("SPIRE_MANAGED_REFERENCE_CYCLE_CHILD"),
                "1",
                StringComparison.Ordinal))
        {
            RunCycleProbe();
            return;
        }

        var project = FindProjectFile();
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test \"{project}\" --no-build --no-restore --filter \"FullyQualifiedName~{typeof(UnityManagedReferenceBehaviorTests).FullName}.Factory_CyclicGraphDoesNotCloneOrRecurseForever\" --logger \"console;verbosity=minimal\"",
            WorkingDirectory = Directory.GetParent(project)!.Parent!.Parent!.FullName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.Environment["SPIRE_MANAGED_REFERENCE_CYCLE_CHILD"] = "1";
        using var process = Process.Start(info);
        Assert.NotNull(process);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            Assert.Fail("Managed-reference cycle probe timed out.");
        }

        var output = await outputTask;
        var error = await errorTask;
        Assert.True(
            process.ExitCode == 0,
            "Managed-reference cycle probe failed.\nstdout:\n" + output + "\nstderr:\n" + error);
#endif
    }

    private static void AssertManagedReferenceCycleMetadata(Assembly assembly)
    {
        var unionType = ManagedReferenceTestSupport.RequireType(
            assembly,
            "ManagedReferenceAdditiveUnion");
        var payloadType = ManagedReferenceTestSupport.FindPayloadType(
            unionType,
            new[] { "value" },
            "Reference");
        Assert.True(
            ManagedReferenceTestSupport.HasAttribute(
                ManagedReferenceTestSupport.GetField(payloadType, "value"),
                "UnityEngine.SerializeReference"));
    }

    private static void RunCycleProbe()
    {
        var result = ManagedReferenceTestSupport.Run();
        var assembly = ManagedReferenceTestSupport.Emit(result, "managed-reference cycle probe");
        var nodeType = ManagedReferenceTestSupport.RequireType(assembly, "ManagedReferenceCycleNode");
        var unionType = ManagedReferenceTestSupport.RequireType(assembly, "ManagedReferenceAdditiveUnion");
        var root = Activator.CreateInstance(nodeType)!;
        ManagedReferenceTestSupport.SetField(root, "next", root);
        ManagedReferenceTestSupport.SetField(
            root,
            "union",
            ManagedReferenceTestSupport.InvokeFactory(unionType, "Reference", root));

        var prepared = UnitySerializationBridge.Prepare(root)!;
        var preparedUnion = ManagedReferenceTestSupport.GetFieldValue(prepared, "union")!;

        Assert.Same(root, ManagedReferenceTestSupport.GetFieldValue(prepared, "next"));
        Assert.Same(root, ManagedReferenceTestSupport.GetPropertyValue(preparedUnion, "value"));
    }

    private static void AssertManagedField(
        Type unionType,
        string variantName,
        string[] fieldNames,
        bool managedReference)
    {
        var payloadType = ManagedReferenceTestSupport.FindPayloadType(
            unionType,
            fieldNames,
            variantName);
        foreach (var fieldName in fieldNames)
        {
            var field = ManagedReferenceTestSupport.GetField(payloadType, fieldName);
            Assert.True(ManagedReferenceTestSupport.HasAttribute(field, "UnityEngine.SerializeField"));
            Assert.Equal(
                managedReference,
                ManagedReferenceTestSupport.HasAttribute(field, "UnityEngine.SerializeReference"));
        }
    }

#if !SPIRE_UNITY_TEST_HOST
    private static string FindProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tests",
                "Houtamelo.Spire.SourceGenerators.Tests",
                "Houtamelo.Spire.SourceGenerators.Tests.csproj");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent!;
        }

        throw new FileNotFoundException("Source-generator test project was not found.");
    }
#endif
}

internal static class ManagedReferenceTestSupport
{
    internal static readonly string[] LayoutUnionNames =
    {
        "ManagedReferenceAdditiveUnion",
        "ManagedReferenceBoxedFieldsUnion",
        "ManagedReferenceBoxedTupleUnion",
        "ManagedReferenceUnsafeOverlapUnion",
        "ManagedReferenceOverlapUnion",
    };

    internal const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;
        using UnityEngine;
        using SpireManagedReference = Houtamelo.Spire.UnitySerializeReferenceAttribute;

        namespace Foreign
        {
            [AttributeUsage(AttributeTargets.Parameter)]
            public sealed class UnitySerializeReferenceAttribute : Attribute { }
        }

        [Serializable]
        public class NodeBase : INode
        {
            [SerializeReference]
            public NodeBase? next;
            public string? name;
        }

        public interface INode { }

        [Serializable]
        public sealed class NodeLeaf : NodeBase
        {
            public int leaf;
        }

        [Serializable]
        public sealed class NodeOther : NodeBase
        {
            public float other;
        }

        [Serializable]
        public class ManagedReferenceDynamicBase : NodeBase
        {
        }

        public interface IManagedReferenceDynamicNode { }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceDynamicChildUnion
        {
            [Variant] public static partial ManagedReferenceDynamicChildUnion Value(
                int childValue);
            [Variant] public static partial ManagedReferenceDynamicChildUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceFieldlessChildUnion
        {
            [Variant] public static partial ManagedReferenceFieldlessChildUnion EmptyA();
            [Variant] public static partial ManagedReferenceFieldlessChildUnion EmptyB();
            [Variant] public static partial ManagedReferenceFieldlessChildUnion Value(int childValue);
        }

        [Serializable]
        public sealed class ManagedReferenceFieldlessHost
        {
            public ManagedReferenceFieldlessChildUnion child;
        }

        [Serializable]
        public sealed class ManagedReferenceCallbackHelper : ISerializationCallbackReceiver
        {
            public ManagedReferenceDynamicChildUnion child;
            [NonSerialized] public int beforeCount;
            [NonSerialized] public int afterCount;

            public void OnBeforeSerialize() => beforeCount++;
            public void OnAfterDeserialize() => afterCount++;
        }

        [Serializable]
        public sealed class ManagedReferenceDynamicLeaf :
            ManagedReferenceDynamicBase,
            IManagedReferenceDynamicNode
        {
            public int leaf;
            public ManagedReferenceDynamicChildUnion child;
        }

        [Serializable]
        public sealed class ManagedReferenceMixedAliasHost
        {
            public ManagedReferenceAdditiveUnion inline;
            public ManagedReferenceAdditiveUnion managed;
        }

        [Serializable]
        public sealed class ManagedReferenceCycleNode : NodeBase
        {
            public ManagedReferenceAdditiveUnion union;
        }

        [Serializable]
        public sealed class ManagedReferenceHelper
        {
            public ManagedReferenceAdditiveUnion child;
            [SerializeReference] private NodeBase? node;
            [SerializeReference] private object? opaque;
            [SerializeReference] private ManagedReferenceDynamicBase? dynamicBase;
            [SerializeReference] private IManagedReferenceDynamicNode? dynamicInterface;
            [SerializeReference] private object? dynamicObject;
            public ManagedReferenceHelper? next;
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceAdditiveUnion
        {
            [Variant] public static partial ManagedReferenceAdditiveUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceAdditiveUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceAdditiveUnion Pair(
                [UnitySerializeReference] NodeBase? first,
                [UnitySerializeReference] NodeBase? second);
            [Variant] public static partial ManagedReferenceAdditiveUnion Array(
                [UnitySerializeReference] NodeBase[] arrayValues);
            [Variant] public static partial ManagedReferenceAdditiveUnion List(
                [UnitySerializeReference] List<NodeBase> listValues);
            [Variant] public static partial ManagedReferenceAdditiveUnion Nested(
                ManagedReferenceHelper helper);
            [Variant] public static partial ManagedReferenceAdditiveUnion Callback(
                [UnitySerializeReference] ManagedReferenceCallbackHelper callback);
            [Variant] public static partial ManagedReferenceAdditiveUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedFields)]
        public partial struct ManagedReferenceBoxedFieldsUnion
        {
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Pair(
                [UnitySerializeReference] NodeBase? first,
                [UnitySerializeReference] NodeBase? second);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Array(
                [UnitySerializeReference] NodeBase[] arrayValues);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion List(
                [UnitySerializeReference] List<NodeBase> listValues);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Nested(
                ManagedReferenceHelper helper);
            [Variant] public static partial ManagedReferenceBoxedFieldsUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct ManagedReferenceBoxedTupleUnion
        {
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Pair(
                [UnitySerializeReference] NodeBase? first,
                [UnitySerializeReference] NodeBase? second);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Array(
                [UnitySerializeReference] NodeBase[] arrayValues);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion List(
                [UnitySerializeReference] List<NodeBase> listValues);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Nested(
                ManagedReferenceHelper helper);
            [Variant] public static partial ManagedReferenceBoxedTupleUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct ManagedReferenceUnsafeOverlapUnion
        {
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Pair(
                [UnitySerializeReference] NodeBase? first,
                [UnitySerializeReference] NodeBase? second);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Array(
                [UnitySerializeReference] NodeBase[] arrayValues);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion List(
                [UnitySerializeReference] List<NodeBase> listValues);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Nested(
                ManagedReferenceHelper helper);
            [Variant] public static partial ManagedReferenceUnsafeOverlapUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct ManagedReferenceOverlapUnion
        {
            [Variant] public static partial ManagedReferenceOverlapUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceOverlapUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceOverlapUnion Pair(
                [UnitySerializeReference] NodeBase? first,
                [UnitySerializeReference] NodeBase? second);
            [Variant] public static partial ManagedReferenceOverlapUnion Array(
                [UnitySerializeReference] NodeBase[] arrayValues);
            [Variant] public static partial ManagedReferenceOverlapUnion List(
                [UnitySerializeReference] List<NodeBase> listValues);
            [Variant] public static partial ManagedReferenceOverlapUnion Nested(
                ManagedReferenceHelper helper);
            [Variant] public static partial ManagedReferenceOverlapUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceRenamedUnion
        {
            [Variant(FormerlySerializedAs = "OldReference")]
            public static partial ManagedReferenceRenamedUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceRenamedUnion Inline(NodeBase? value);
            [Variant] public static partial ManagedReferenceRenamedUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceGeneric<T>
        {
            [Variant] public static partial ManagedReferenceGeneric<T> Value(
                [UnitySerializeReference] T value);
            [Variant] public static partial ManagedReferenceGeneric<T> Inline(T value);
            [Variant] public static partial ManagedReferenceGeneric<T> Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceBaseInterfaceUnion
        {
            [Variant] public static partial ManagedReferenceBaseInterfaceUnion Base(
                [UnitySerializeReference] NodeBase? baseValue);
            [Variant] public static partial ManagedReferenceBaseInterfaceUnion Interface(
                [UnitySerializeReference] INode? interfaceValue);
            [Variant] public static partial ManagedReferenceBaseInterfaceUnion Derived(
                [UnitySerializeReference] NodeLeaf? derivedValue);
            [Variant] public static partial ManagedReferenceBaseInterfaceUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceEligibilityUnion
        {
            [Variant] public static partial ManagedReferenceEligibilityUnion Sensitive(
                [UnitySerializeReference] object? objectValue,
                [UnitySerializeReference] NodeBase? baseValue,
                [UnitySerializeReference] INode? interfaceValue,
                [UnitySerializeReference] Dictionary<int, string>? map,
                [UnitySerializeReference] int[,]? matrix,
                [UnitySerializeReference] UnityEngine.Object? asset);
            [Variant] public static partial ManagedReferenceEligibilityUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceBindingUnion
        {
            [Variant] public static partial ManagedReferenceBindingUnion Qualified(
                [global::Houtamelo.Spire.UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceBindingUnion Aliased(
                [SpireManagedReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceBindingUnion Normal(NodeBase? value);
            [Variant] public static partial ManagedReferenceBindingUnion Foreign(
                [Foreign.UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceBindingUnion Empty();
        }

        [DiscriminatedUnion(Layout.Additive)]
        public partial struct ManagedReferenceNonUnityUnion
        {
            [Variant] public static partial ManagedReferenceNonUnityUnion Reference(
                [UnitySerializeReference] NodeBase? value);
            [Variant] public static partial ManagedReferenceNonUnityUnion Empty();
        }
        """;

    internal static UnityGeneratorRun Run()
    {
        var result = UnityGeneratorTestHarness.Run(Source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return result;
    }

    internal static Assembly Emit(UnityGeneratorRun result, string description)
        => UnityGeneratorTestHarness.EmitAndLoad(
            Assert.IsType<CSharpCompilation>(result.OutputCompilation),
            description);

    internal static Type RequireType(Assembly assembly, string metadataName)
    {
        var type = assembly.GetType(metadataName);
        Assert.NotNull(type);
        return type!;
    }

    internal static FieldInfo GetField(Type type, string name)
    {
        var field = type.GetField(name, InstanceFlags);
        Assert.NotNull(field);
        return field!;
    }

    internal static FieldInfo? GetFieldOrNull(Type type, string name)
        => type.GetField(name, InstanceFlags);

    internal static object InvokeFactory(Type unionType, string name, params object?[] arguments)
    {
        var method = unionType.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var value = method!.Invoke(null, arguments);
        Assert.NotNull(value);
        return value!;
    }

    internal static object InvokeFromSerialized(Type unionType, object? payload)
    {
        var methods = unionType
            .GetMethods(InstanceFlags | BindingFlags.Static)
            .Where(method => method.Name == "__SpireFromSerialized")
            .Where(method => method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(object))
            .ToArray();
        Assert.Single(methods);
        var method = methods[0];
        var value = method.Invoke(
            method.IsStatic ? null : Activator.CreateInstance(unionType),
            new[] { payload });
        Assert.NotNull(value);
        return value!;
    }

    internal static void InvokeCallback(object value, string name)
    {
        var method = value.GetType().GetMethod(name, InstanceFlags);
        Assert.NotNull(method);
        method!.Invoke(value, null);
    }

    internal static object? GetPayload(object value)
        => GetField(value.GetType(), "_serializedPayload").GetValue(value);

    internal static string GetKindName(object value)
        => GetPropertyValue(value, "kind")!.ToString()!;

    internal static object? GetPropertyValue(object value, string name)
    {
        var property = value.GetType().GetProperty(name, InstanceFlags);
        Assert.NotNull(property);
        return property!.GetValue(value);
    }

    internal static object? GetFieldValue(object value, string name)
        => GetField(value.GetType(), name).GetValue(value);

    internal static void SetField(object value, string name, object? fieldValue)
        => GetField(value.GetType(), name).SetValue(value, fieldValue);

    internal static void SetPropertyValue(object value, string name, object? propertyValue)
    {
        var property = value.GetType().GetProperty(name, InstanceFlags);
        Assert.NotNull(property);
        Assert.NotNull(property!.SetMethod);
        property.SetValue(value, propertyValue);
    }

    internal static Type[] ConcretePayloadTypes(Type unionType)
        => unionType.GetNestedTypes(InstanceFlags)
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.IsDefined(typeof(SerializableAttribute), inherit: false))
            .ToArray();

    internal static Type FindPayloadType(
        Type unionType,
        string[] fieldNames,
        string? nameContains = null)
    {
        var candidates = ConcretePayloadTypes(unionType)
            .Where(type => nameContains is null ||
                type.Name.Contains(
                    "__Spire" + nameContains + "Payload",
                    StringComparison.Ordinal))
            .Where(type => DeclaredInstanceFields(type)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(fieldNames.OrderBy(name => name, StringComparer.Ordinal)))
            .ToArray();
        Assert.Single(candidates);
        return candidates[0];
    }

    internal static FieldInfo[] DeclaredInstanceFields(Type type)
        => type.GetFields(InstanceFlags | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsStatic)
            .ToArray();

    internal static bool HasAttribute(MemberInfo member, string fullName)
        => member.GetCustomAttributesData().Any(attribute =>
            string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal));
}
