#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    public sealed class InspectorCommandComponent : MonoBehaviour
    {
        public InspectorCommand command;
        public InspectorCommand copiedLeft;
        public InspectorCommand copiedRight;
        public InspectorCommand[] commands = Array.Empty<InspectorCommand>();
        public List<InspectorCommand> commandList = new List<InspectorCommand>();
        public string ordinaryField = string.Empty;
        public Texture2D? sharedTexture;

        [SerializeReference]
        public InspectorNode? sharedNode;

        [SerializeReference]
        public InspectorNode? secondSharedNode;
    }

}
