#nullable enable

using System;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class InspectorGenericAsset : ScriptableObject
    {
        public InspectorGeneric<int> generic;
    }

}
