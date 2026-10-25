#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    [Serializable]
    public abstract class UnityManagedReferenceNodeBase
    {
        public string id = string.Empty;

        [SerializeReference]
        public UnityManagedReferenceNodeBase? next;
    }

    [Serializable]
    public sealed class UnityManagedReferenceNodeLeaf : UnityManagedReferenceNodeBase, IUnityManagedReferenceNode
    {
        public int amount;
    }

    [Serializable]
    public sealed class UnityManagedReferenceNodeOther : UnityManagedReferenceNodeBase, IUnityManagedReferenceNode
    {
        public string label = string.Empty;
    }

    public interface IUnityManagedReferenceNode
    {
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceNodeStateUnion
    {
        [Variant]
        public static partial UnityManagedReferenceNodeStateUnion Active(int marker);

        [Variant]
        public static partial UnityManagedReferenceNodeStateUnion Empty();
    }

    [Serializable]
    public sealed class UnityManagedReferenceNodeGeneratedState : UnityManagedReferenceNodeBase, IUnityManagedReferenceNode
    {
        public UnityManagedReferenceNodeStateUnion state;

        [SerializeReference]
        private object? objectHolder;

        [SerializeReference]
        private IUnityManagedReferenceNode? interfaceHolder;

        public object? ObjectHolder => objectHolder;

        public IUnityManagedReferenceNode? InterfaceHolder => interfaceHolder;

        public void SetGeneratedState(
            UnityManagedReferenceNodeStateUnion stateValue,
            object? objectValue,
            IUnityManagedReferenceNode? interfaceValue)
        {
            state = stateValue;
            objectHolder = objectValue;
            interfaceHolder = interfaceValue;
        }
    }

    [Serializable]
    public sealed class UnityManagedReferenceMixedAliasHelper
    {
        public UnityManagedReferenceNodeGeneratedState inlineNode = new UnityManagedReferenceNodeGeneratedState();

        [SerializeReference]
        private UnityManagedReferenceNodeBase? managedNode;

        public UnityManagedReferenceNodeBase? ManagedNode => managedNode;

        public void SetManagedNode(UnityManagedReferenceNodeBase? node)
        {
            managedNode = node;
        }
    }

    [Serializable]
    public sealed class UnityManagedReferenceHelper
    {
        public UnityManagedReferenceAdditiveUnion child;

        [SerializeReference]
        private UnityManagedReferenceNodeBase? node;

        [SerializeReference]
        private object? opaque;

        [SerializeReference]
        private UnityManagedReferenceHelper? next;

        public UnityManagedReferenceNodeBase? Node => node;

        public object? Opaque => opaque;

        public UnityManagedReferenceHelper? Next => next;

        public UnityManagedReferenceHelper()
        {
        }

        public UnityManagedReferenceHelper(
            UnityManagedReferenceAdditiveUnion child,
            UnityManagedReferenceNodeBase? node,
            object? opaque,
            UnityManagedReferenceHelper? next)
        {
            this.child = child;
            this.node = node;
            this.opaque = opaque;
            this.next = next;
        }

        public void SetReferences(
            UnityManagedReferenceNodeBase? nodeValue,
            object? opaqueValue,
            UnityManagedReferenceHelper? nextValue)
        {
            node = nodeValue;
            opaque = opaqueValue;
            next = nextValue;
        }
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceAdditiveUnion
    {
        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Pair(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? first,
            [UnitySerializeReference] UnityManagedReferenceNodeBase? second);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Array(
            [UnitySerializeReference] UnityManagedReferenceNodeBase?[] arrayValues);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion List(
            [UnitySerializeReference] List<UnityManagedReferenceNodeBase?> listValues);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Nested(UnityManagedReferenceHelper? helper);

        [Variant]
        public static partial UnityManagedReferenceAdditiveUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceBoxedFieldsUnion
    {
        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Pair(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? first,
            [UnitySerializeReference] UnityManagedReferenceNodeBase? second);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Array(
            [UnitySerializeReference] UnityManagedReferenceNodeBase?[] arrayValues);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion List(
            [UnitySerializeReference] List<UnityManagedReferenceNodeBase?> listValues);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion MixedAlias(UnityManagedReferenceMixedAliasHelper mixed);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Nested(UnityManagedReferenceHelper? helper);

        [Variant]
        public static partial UnityManagedReferenceBoxedFieldsUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceBoxedTupleUnion
    {
        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Pair(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? first,
            [UnitySerializeReference] UnityManagedReferenceNodeBase? second);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Array(
            [UnitySerializeReference] UnityManagedReferenceNodeBase?[] arrayValues);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion List(
            [UnitySerializeReference] List<UnityManagedReferenceNodeBase?> listValues);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Nested(UnityManagedReferenceHelper? helper);

        [Variant]
        public static partial UnityManagedReferenceBoxedTupleUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceUnsafeOverlapUnion
    {
        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Pair(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? first,
            [UnitySerializeReference] UnityManagedReferenceNodeBase? second);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Array(
            [UnitySerializeReference] UnityManagedReferenceNodeBase?[] arrayValues);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion List(
            [UnitySerializeReference] List<UnityManagedReferenceNodeBase?> listValues);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Nested(UnityManagedReferenceHelper? helper);

        [Variant]
        public static partial UnityManagedReferenceUnsafeOverlapUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceOverlapUnion
    {
        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Pair(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? first,
            [UnitySerializeReference] UnityManagedReferenceNodeBase? second);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Array(
            [UnitySerializeReference] UnityManagedReferenceNodeBase?[] arrayValues);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion List(
            [UnitySerializeReference] List<UnityManagedReferenceNodeBase?> listValues);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Nested(UnityManagedReferenceHelper? helper);

        [Variant]
        public static partial UnityManagedReferenceOverlapUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceRenamedUnion
    {
        [Variant(FormerlySerializedAs = "OldReference")]
        public static partial UnityManagedReferenceRenamedUnion Reference(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceRenamedUnion Inline(UnityManagedReferenceNodeBase? value);

        [Variant]
        public static partial UnityManagedReferenceRenamedUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceGeneric<T>
    {
        [Variant]
        public static partial UnityManagedReferenceGeneric<T> Value([UnitySerializeReference] T value);

        [Variant]
        public static partial UnityManagedReferenceGeneric<T> Inline(T value);

        [Variant]
        public static partial UnityManagedReferenceGeneric<T> Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityManagedReferenceBaseInterfaceUnion
    {
        [Variant]
        public static partial UnityManagedReferenceBaseInterfaceUnion Base(
            [UnitySerializeReference] UnityManagedReferenceNodeBase? baseValue);

        [Variant]
        public static partial UnityManagedReferenceBaseInterfaceUnion Interface(
            [UnitySerializeReference] IUnityManagedReferenceNode? interfaceValue);

        [Variant]
        public static partial UnityManagedReferenceBaseInterfaceUnion Derived(
            [UnitySerializeReference] UnityManagedReferenceNodeLeaf? derivedValue);

        [Variant]
        public static partial UnityManagedReferenceBaseInterfaceUnion Empty();
    }

    [Serializable]
    public sealed class UnityManagedReferenceHostData
    {
        public UnityManagedReferenceAdditiveUnion additive;
        public UnityManagedReferenceBoxedFieldsUnion boxedFields;
        public UnityManagedReferenceBoxedTupleUnion boxedTuple;
        public UnityManagedReferenceUnsafeOverlapUnion unsafeOverlap;
        public UnityManagedReferenceOverlapUnion overlap;
        public UnityManagedReferenceRenamedUnion renamed;
        public UnityManagedReferenceGeneric<UnityManagedReferenceNodeBase> genericBase;
        public UnityManagedReferenceGeneric<IUnityManagedReferenceNode> genericInterface;
        public UnityManagedReferenceGeneric<UnityManagedReferenceNodeLeaf> genericDerived;
        public UnityManagedReferenceBaseInterfaceUnion baseInterface;
        [SerializeReference]
        public UnityManagedReferenceNodeBase? sharedNode;

        [SerializeReference]
        public UnityManagedReferenceNodeBase? cycleRoot;

        [SerializeReference]
        public UnityManagedReferenceNodeGeneratedState? generatedStateNode;

        [SerializeReference]
        public UnityManagedReferenceNodeBase? dynamicNode;

        [SerializeReference]
        public UnityManagedReferenceNodeBase?[] nodeArray = Array.Empty<UnityManagedReferenceNodeBase?>();

        [SerializeReference]
        public List<UnityManagedReferenceNodeBase?> nodeList = new List<UnityManagedReferenceNodeBase?>();
        [SerializeReference]
        public UnityManagedReferenceHelper helper = new UnityManagedReferenceHelper();

        [SerializeReference]
        private UnityManagedReferenceNodeBase? privateNode;

        [SerializeReference]
        private object? opaque;

        public UnityManagedReferenceNodeBase? PrivateNode => privateNode;

        public object? Opaque => opaque;

        public void SetPrivateReferences(UnityManagedReferenceNodeBase? node, object? opaqueValue)
        {
            privateNode = node;
            opaque = opaqueValue;
        }
    }

}
