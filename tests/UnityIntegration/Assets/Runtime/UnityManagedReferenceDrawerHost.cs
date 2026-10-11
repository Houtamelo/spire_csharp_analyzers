#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class UnityManagedReferenceDrawerHost : MonoBehaviour
    {
        public UnityManagedReferenceAdditiveUnion value;
        public UnityManagedReferenceAdditiveUnion[] array = Array.Empty<UnityManagedReferenceAdditiveUnion>();
        public List<UnityManagedReferenceAdditiveUnion> list = new List<UnityManagedReferenceAdditiveUnion>();
        public UnityManagedReferenceHelper helper = new UnityManagedReferenceHelper();
    }
}
