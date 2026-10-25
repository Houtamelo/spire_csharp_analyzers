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

}
