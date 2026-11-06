#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class ConcreteWrapperGraphNode
    {
        public string name = string.Empty;
        public int value;

        [SerializeReference]
        public ConcreteWrapperGraphNode? next;
    }

    [Serializable]
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct ConcreteWrapperShape
    {
        [Variant]
        public static partial ConcreteWrapperShape Move(Vector2 destination, float speed);

        [Variant]
        public static partial ConcreteWrapperShape Speak(string speech);

        [Variant]
        public static partial ConcreteWrapperShape Empty();
    }

    [Serializable]
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct ConcreteWrapperFieldMatrix
    {
        [Variant]
        public static partial ConcreteWrapperFieldMatrix Rich(
            int count,
            string label,
            Texture2D? asset,
            [UnitySerializeReference] ConcreteWrapperGraphNode? graph,
            List<int> values);

        [Variant]
        public static partial ConcreteWrapperFieldMatrix Empty();
    }

    [Serializable]
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct ConcreteWrapperAlias
    {
        [Variant(FormerlySerializedAs = "Old")]
        public static partial ConcreteWrapperAlias Current(int amount, string label);

        [Variant(FormerlySerializedAs = "PriorEmpty")]
        public static partial ConcreteWrapperAlias Empty();

        [Variant]
        public static partial ConcreteWrapperAlias Other(string label);
    }

    public static partial class ConcreteWrapperNestedOwner
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

}
