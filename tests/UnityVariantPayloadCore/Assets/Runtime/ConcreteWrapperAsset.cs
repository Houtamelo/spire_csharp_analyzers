#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class ConcreteWrapperAsset : ScriptableObject
    {
        public ConcreteWrapperShape shape;
        public ConcreteWrapperShape speech;
        public ConcreteWrapperShape empty;
        public ConcreteWrapperShape[] shapeArray = Array.Empty<ConcreteWrapperShape>();
        public List<ConcreteWrapperShape> shapeList = new List<ConcreteWrapperShape>();
        public ConcreteWrapperFieldMatrix rich;
        public ConcreteWrapperAlias alias;
        public ConcreteWrapperNestedOwner.Command command;
        public Texture2D? sharedTexture;

        [SerializeReference]
        public ConcreteWrapperGraphNode? graphRoot;
    }

}
