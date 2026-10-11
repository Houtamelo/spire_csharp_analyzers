#if UNITY_EDITOR
#nullable enable

using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnityDrawerNumericAsset : ScriptableObject
    {
        public UnityDrawerNumericUnion value;
        public UnityDrawerInspectorNamedEnumUnion named;
        public UnityDrawerFlagsUnion flags;
        public int unrelated;
    }
}
#endif
