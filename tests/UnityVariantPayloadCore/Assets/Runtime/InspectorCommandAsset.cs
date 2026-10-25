#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class InspectorCommandAsset : ScriptableObject
    {
        public InspectorCommand command;
        public InspectorCommand copiedLeft;
        public InspectorCommand copiedRight;
        public string ordinaryField = string.Empty;
        public InspectorCommand[] commands = Array.Empty<InspectorCommand>();
        public List<InspectorCommand> commandList = new List<InspectorCommand>();
        public Texture2D? sharedTexture;

        [SerializeReference]
        public InspectorNode? sharedNode;

        [SerializeReference]
        public InspectorNode? secondSharedNode;
    }

}
