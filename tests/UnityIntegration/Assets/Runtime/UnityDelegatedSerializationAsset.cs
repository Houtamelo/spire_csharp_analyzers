#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnityDelegatedSerializationUnion
    {
        [Variant]
        public static partial UnityDelegatedSerializationUnion Value(
            int marker,
            Dictionary<int, string> unsupported);

        [Variant]
        public static partial UnityDelegatedSerializationUnion Empty();
    }

    public sealed class UnityDelegatedSerializationAsset : ScriptableObject
    {
        public UnityDelegatedSerializationUnion union;

        [SerializeField]
        private Dictionary<int, string>? control;

        [SerializeField]
        public int supportedControl;

        public Dictionary<int, string>? Control => control;
    }
}
