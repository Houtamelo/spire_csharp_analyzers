#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityIntegration
{

    public sealed class UnitySerializationAsset : ScriptableObject
    {
        public UnityShapeSerializable single;
        public UnityCollectionUnion[] array = Array.Empty<UnityCollectionUnion>();
        public List<UnityCollectionUnion> list = new List<UnityCollectionUnion>();
        public UnityCallbackParent parent;
        public UnityCopyProbe original;
        public UnityCopyProbe sharedCopy;
        public UnityCopyProbe detachedCopy;
        public UnityAllowedCallbackUnion callbackControl;
        public UnityCopyProbe nestedOriginal;
        public UnityCopyProbe nestedCopy;
        public UnityGenericUnion<int> genericInt;
        public UnityGenericUnion<string> genericString;
        public UnityGenericUnion<UnityCallbackChild> genericChild;
        public UnityGenericUnion<UnityGenericChildHelper> genericHelper;
        public UnityGenericContainer<int>.NestedUnion genericContainerNested;
        public UnityBoxedFieldsUnion boxedFields;
        public UnityBoxedTupleUnion boxedTuple;
        public UnityBoxedFieldsGeneric<int> boxedFieldsGenericInt;
        public UnityBoxedFieldsGeneric<string> boxedFieldsGenericString;
        public UnityBoxedTupleGeneric<int> boxedTupleGenericInt;
        public UnityBoxedTupleGeneric<string> boxedTupleGenericString;
        public UnityBoxedLayoutParent boxedParent;
        public UnityBoxedLayoutParent boxedParentCopy;
        public UnityBoxedFieldsUnion[] boxedFieldsArray = Array.Empty<UnityBoxedFieldsUnion>();
        public List<UnityBoxedTupleUnion> boxedTupleList = new List<UnityBoxedTupleUnion>();
        public UnityUnsafeOverlapUnion unsafeUnion;
        public UnityUnsafeOverlapGeneric<int> unsafeGenericInt;
        public UnityUnsafeOverlapGeneric<string> unsafeGenericString;
        public UnityUnsafeOverlapParent unsafeParent;
        public UnityUnsafeOverlapParent unsafeParentCopy;
        public UnityUnsafeOverlapUnion[] unsafeArray = Array.Empty<UnityUnsafeOverlapUnion>();
        public List<UnityUnsafeOverlapUnion> unsafeList = new List<UnityUnsafeOverlapUnion>();
        public UnityOverlapUnion overlapUnion;
        public UnityOverlapReferenceUnion overlapReference;
        public UnityOverlapParent overlapParent;
        public UnityOverlapParent overlapParentCopy;
        public UnityOverlapUnion[] overlapArray = Array.Empty<UnityOverlapUnion>();
        public List<UnityOverlapReferenceUnion> overlapList = new List<UnityOverlapReferenceUnion>();
        public UnityReferenceComposite referenceCompositeOriginal = null!;
        public UnityReferenceComposite referenceCompositeCopy = null!;
        public Texture2D reference = null!;
        public UnityNestedOwnerData nested;
        public UnityCompositeDerived composite = null!;
        public StabilityEditorHost stability = new StabilityEditorHost();
    }

}
