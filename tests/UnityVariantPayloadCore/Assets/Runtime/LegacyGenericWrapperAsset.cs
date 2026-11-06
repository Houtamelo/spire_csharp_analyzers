#nullable enable

using System;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [Serializable]
    public sealed class LegacyGenericWrapperAsset : ScriptableObject
    {
        public ConcreteWrapperShape shape;
    }

}
