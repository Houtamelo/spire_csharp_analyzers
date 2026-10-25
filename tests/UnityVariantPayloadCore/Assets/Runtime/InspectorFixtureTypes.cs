#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    /// <summary>
    /// A managed-reference node used to prove that the generated serialization
    /// wrapper does not clone user-owned graphs while it isolates its own cache.
    /// </summary>
    [Serializable]
    public class InspectorNode
    {
        public string label = string.Empty;
        public int value;

        [SerializeReference]
        public InspectorNode? next;
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct InspectorCommand
    {
        [Variant]
        public static partial InspectorCommand Idle();

        [Variant]
        public static partial InspectorCommand Speak(string speech);

        [Variant]
        public static partial InspectorCommand Move(Vector2 destination, float speed);

        [Variant]
        public static partial InspectorCommand ManagedRefNode(
            [UnitySerializeReference] InspectorNode? node,
            Texture2D? icon);
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct InspectorGeneric<T>
        where T : unmanaged
    {
        [Variant]
        public static partial InspectorGeneric<T> Value(T value);

        [Variant]
        public static partial InspectorGeneric<T> Empty();
    }

}
