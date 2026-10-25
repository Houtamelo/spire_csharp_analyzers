#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class SerializedGraphNode
    {
        public string name = string.Empty;
        public int value;

        [SerializeReference]
        public SerializedGraphNode? next;
    }

    [Serializable]
    public struct SerializedInlineHelper
    {
        public string label;
        public SerializedNestedOwner.Command command;
        public SerializedShape nested;
        public SerializedCallbackHelper callback;
    }

    [Serializable]
    public sealed class SerializedCallbackHelper : ISerializationCallbackReceiver
    {
        public int marker;
        public SerializedShape nested;

        [NonSerialized]
        public int beforeSerializeCount;

        [NonSerialized]
        public int afterDeserializeCount;

        public void OnBeforeSerialize()
            => beforeSerializeCount++;

        public void OnAfterDeserialize()
            => afterDeserializeCount++;
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedChildUnion
    {
        [Variant]
        public static partial SerializedChildUnion Leaf(int marker, string label);

        [Variant]
        public static partial SerializedChildUnion Command(SerializedNestedOwner.Command value);

        [Variant]
        public static partial SerializedChildUnion Empty();
    }

    public static partial class SerializedNestedOwner
    {
        [Serializable]
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant]
            public static partial Command Run(float speed);

            [Variant]
            public static partial Command Stop();
        }
    }

    [Serializable]
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct SerializedShape
    {
        [Variant]
        public static partial SerializedShape Move(Vector2 destination, float speed);

        [Variant]
        public static partial SerializedShape Empty();

        [Variant]
        public static partial SerializedShape Rich(
            [UnitySerializeReference] SerializedGraphNode? graph,
            Texture2D? icon,
            SerializedInlineHelper helper,
            SerializedChildUnion child,
            SerializedChildUnion[] children,
            List<SerializedChildUnion> childList);
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedGeneric<T>
        where T : unmanaged
    {
        [Variant]
        public static partial SerializedGeneric<T> Value(T value, string label);

        [Variant]
        public static partial SerializedGeneric<T> Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedAliasUnion
    {
        [Variant(FormerlySerializedAs = "Former")]
        public static partial SerializedAliasUnion Current(int amount, string label);

        [Variant]
        public static partial SerializedAliasUnion Empty();
    }

    // These paired unions intentionally have the same user-declared instance
    // fields. The player probe compares their generated field sets to ensure
    // Unity serialization contributes only the managed-reference payload cache.
    [DiscriminatedUnion]
    public partial struct PlainSerializationState
    {
        public int userField;

        [Variant]
        public static partial PlainSerializationState First(int value);

        [Variant]
        public static partial PlainSerializationState Text(string text);

        [Variant]
        public static partial PlainSerializationState Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedSerializationState
    {
        public int userField;

        [Variant]
        public static partial SerializedSerializationState First(int value);

        [Variant]
        public static partial SerializedSerializationState Text(string text);

        [Variant]
        public static partial SerializedSerializationState Empty();
    }

    [DiscriminatedUnion]
    public partial struct PlainReferenceState
    {
        public int userField;

        [Variant]
        public static partial PlainReferenceState Reference(string text);

        [Variant]
        public static partial PlainReferenceState Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedReferenceState
    {
        public int userField;

        [Variant]
        public static partial SerializedReferenceState Reference(string text);

        [Variant]
        public static partial SerializedReferenceState Empty();
    }

    [DiscriminatedUnion]
    public partial struct PlainGenericState<T>
        where T : unmanaged
    {
        public int userField;

        [Variant]
        public static partial PlainGenericState<T> Value(T value);

        [Variant]
        public static partial PlainGenericState<T> Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct SerializedGenericState<T>
        where T : unmanaged
    {
        public int userField;

        [Variant]
        public static partial SerializedGenericState<T> Value(T value);

        [Variant]
        public static partial SerializedGenericState<T> Empty();
    }

}
