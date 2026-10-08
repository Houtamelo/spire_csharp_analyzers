#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spire.UnityIntegration
{

    [Serializable]
    public struct UnityNestedOwnerData
    {
        public UnityShapeSerializable value;
        public UnityCollectionUnion[] array;
        public List<UnityCollectionUnion> list;

        [SerializeField]
        private UnityShapeSerializable child;

        public UnityShapeSerializable Child => child;

        public UnityNestedOwnerData(
            UnityShapeSerializable value,
            UnityCollectionUnion[] array,
            List<UnityCollectionUnion> list,
            UnityShapeSerializable child)
        {
            this.value = value;
            this.array = array;
            this.list = list;
            this.child = child;
        }
    }

}
