#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityIntegration
{

    public class UnityDrawerBaseHost : MonoBehaviour
    {
        [SerializeField]
        private UnityDrawerUnion privateValue;

        public UnityDrawerUnion PrivateValue => privateValue;
    }

    public sealed class UnityDrawerHost : UnityDrawerBaseHost
    {
        public UnityDrawerUnion single;
        public StabilityDrawerAliasUnion alias;
        public UnityBoxedFieldsUnion boxedFields;
        public UnityBoxedTupleUnion boxedTuple;
        public UnityBoxedFieldsUnion[] boxedFieldsArray = Array.Empty<UnityBoxedFieldsUnion>();
        public List<UnityBoxedTupleUnion> boxedTupleList = new List<UnityBoxedTupleUnion>();
        public UnityUnsafeOverlapUnion unsafeUnion;
        public UnityUnsafeOverlapUnion[] unsafeArray = Array.Empty<UnityUnsafeOverlapUnion>();
        public List<UnityUnsafeOverlapUnion> unsafeList = new List<UnityUnsafeOverlapUnion>();
        public UnityOverlapUnion overlapUnion;
        public UnityOverlapUnion[] overlapArray = Array.Empty<UnityOverlapUnion>();
        public List<UnityOverlapUnion> overlapList = new List<UnityOverlapUnion>();
        public UnityDrawerUnion[] array = Array.Empty<UnityDrawerUnion>();
        public List<UnityDrawerUnion> list = new List<UnityDrawerUnion>();
        public UnityNestedOwnerData nested;
        public UnityGenericUnion<int> genericInt;
        public UnityGenericUnion<string> genericString;
        public UnityGenericContainerNestedUnion genericContainerNested;
        public UnityGenericDrawerParent genericParent;
    }

}
