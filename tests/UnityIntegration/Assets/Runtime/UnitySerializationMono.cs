#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityIntegration
{

    public sealed class UnitySerializationMono : MonoBehaviour
    {
        public UnityShapeSerializable single;
        public UnityCollectionUnion[] array = Array.Empty<UnityCollectionUnion>();
        public List<UnityCollectionUnion> list = new List<UnityCollectionUnion>();
        public UnityCallbackParent parent;
        public UnityCopyProbe original;
        public UnityCopyProbe sharedCopy;
        public UnityCopyProbe detachedCopy;
        public UnityGenericUnion<int> genericInt;
        public UnityGenericUnion<string> genericString;
        public UnityBoxedFieldsUnion boxedFields;
        public UnityBoxedTupleUnion boxedTuple;
        public UnityBoxedFieldsGeneric<int> boxedFieldsGenericInt;
        public UnityBoxedTupleGeneric<string> boxedTupleGenericString;
        public UnityUnsafeOverlapUnion unsafeUnion;
        public UnityUnsafeOverlapGeneric<int> unsafeGenericInt;
        public UnityUnsafeOverlapGeneric<string> unsafeGenericString;
        public UnityOverlapUnion overlapUnion;
        public UnityOverlapReferenceUnion overlapReference;
        public Texture2D reference = null!;
        public UnityNestedOwnerData nested;
    }

}
