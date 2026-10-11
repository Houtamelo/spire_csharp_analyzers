#nullable enable

using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnitySerializationFootprintAsset : ScriptableObject
    {
        public UnitySerializationFootprintCollisionUnion collision;
        public UnitySerializationFootprintNullableUnion nullable;
    }
}
