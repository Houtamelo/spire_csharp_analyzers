#nullable enable

using System;
using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnityDrawerAsset : ScriptableObject
    {
        public UnityDrawerUnion[] values = Array.Empty<UnityDrawerUnion>();
        public int unrelated;
    }
}
