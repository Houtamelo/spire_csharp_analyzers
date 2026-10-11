#nullable enable

using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnityManagedReferencePlayerHost : MonoBehaviour
    {
        public const int FullProbe = 0;
        public const int NullOnlyProbe = 1;
        public const int SharedOnlyProbe = 2;
        public const int CycleOnlyProbe = 3;

        public UnityManagedReferenceHostData data = new UnityManagedReferenceHostData();
        public UnityManagedReferenceHostData second = new UnityManagedReferenceHostData();
        public bool managedReferenceMetadataAvailable;
        public int probeMode = FullProbe;

        [SerializeReference]
        public object? opaqueLegacyRenamed;
    }
}
