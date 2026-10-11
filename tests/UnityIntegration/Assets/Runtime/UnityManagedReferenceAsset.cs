#nullable enable

using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnityManagedReferenceAsset : ScriptableObject
    {
        public UnityManagedReferenceHostData data = new UnityManagedReferenceHostData();
        public UnityManagedReferenceHostData second = new UnityManagedReferenceHostData();
        public UnityManagedReferenceAdditiveUnion aliasFirst;
        public UnityManagedReferenceAdditiveUnion aliasSecond;
    }
}
