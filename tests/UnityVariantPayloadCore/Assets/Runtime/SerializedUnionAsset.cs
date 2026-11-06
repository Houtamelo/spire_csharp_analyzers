#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class SerializedUnionAsset : ScriptableObject
    {
        public SerializedShape shape;
        public SerializedShape empty;
        public SerializedShape nested;
        public SerializedShape[] shapeArray = Array.Empty<SerializedShape>();
        public List<SerializedShape> shapeList = new List<SerializedShape>();
#if UNITY_2022_1_OR_NEWER && !UNITY_2023_1_OR_NEWER
        [NonSerialized]
#endif
        public SerializedGeneric<int> generic;
        public SerializedAliasUnion alias;
        public SerializedNestedOwner.Command command;
        public SerializedShape copiedLeft;
        public SerializedShape copiedRight;
        public SerializedInlineHelper inline;
        public Texture2D? sharedTexture;

        [SerializeReference]
        public SerializedGraphNode? graphRoot;
    }

    public static class SerializedUnionNativeCapabilities
    {
#if UNITY_2022_1_OR_NEWER && !UNITY_2023_1_OR_NEWER
        public const bool GenericUnionSerializationSupported = false;
        public const string GenericUnionSerializationApplicability = "not-applicable-unity-2022";
#else
        public const bool GenericUnionSerializationSupported = true;
        public const string GenericUnionSerializationApplicability = "applicable";
#endif
    }

}
