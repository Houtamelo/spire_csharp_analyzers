#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{

    [Serializable]
    public enum SmallEnum : byte
    {
        Zero,
        First,
        Last,
    }

    [Serializable]
    public struct SerializableData
    {
        public int id;
        public string label;
        public Vector2 position;

        public SerializableData(int id, string label, Vector2 position)
        {
            this.id = id;
            this.label = label;
            this.position = position;
        }
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityShapeSerializable
    {
        [Variant]
        public static partial UnityShapeSerializable Circle(float radius);

        [Variant]
        public static partial UnityShapeSerializable Label(string text, Texture2D icon);

        [Variant]
        public static partial UnityShapeSerializable Point();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityPayloadFieldMatrix
    {
        [Variant]
        public static partial UnityPayloadFieldMatrix Values(
            bool flag,
            int count,
            float amount,
            double precise,
            string label,
            SmallEnum mode);

        [Variant]
        public static partial UnityPayloadFieldMatrix Native(Vector3 position, Color tint, Texture2D texture);

        [Variant]
        public static partial UnityPayloadFieldMatrix Nested(SerializableData data);

        [Variant]
        public static partial UnityPayloadFieldMatrix Collections(int[] samples, List<SerializableData> entries);
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityCallbackChild
    {
        [Variant]
        public static partial UnityCallbackChild Text(int count, string label, Texture2D asset);

        [Variant]
        public static partial UnityCallbackChild Number(float value);

        [Variant]
        public static partial UnityCallbackChild Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityBoxedFieldsUnion
    {
        [Variant]
        public static partial UnityBoxedFieldsUnion Number(int value);

        [Variant]
        public static partial UnityBoxedFieldsUnion Text(string label, Texture2D icon);

        [Variant]
        public static partial UnityBoxedFieldsUnion Child(UnityCallbackChild child);

        [Variant]
        public static partial UnityBoxedFieldsUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityBoxedTupleUnion
    {
        [Variant]
        public static partial UnityBoxedTupleUnion Number(int value);

        [Variant]
        public static partial UnityBoxedTupleUnion Text(string label, Texture2D icon);

        [Variant]
        public static partial UnityBoxedTupleUnion Child(UnityCallbackChild child);

        [Variant]
        public static partial UnityBoxedTupleUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityBoxedFieldsGeneric<T>
    {
        [Variant]
        public static partial UnityBoxedFieldsGeneric<T> Some(T value);

        [Variant]
        public static partial UnityBoxedFieldsGeneric<T> None();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityBoxedTupleGeneric<T>
    {
        [Variant]
        public static partial UnityBoxedTupleGeneric<T> Some(T value);

        [Variant]
        public static partial UnityBoxedTupleGeneric<T> None();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityBoxedLayoutParent
    {
        [Variant]
        public static partial UnityBoxedLayoutParent Parent(UnityBoxedFieldsUnion child, int marker);

        [Variant]
        public static partial UnityBoxedLayoutParent TupleParent(UnityBoxedTupleUnion tupleChild, int marker);

        [Variant]
        public static partial UnityBoxedLayoutParent Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityUnsafeOverlapUnion
    {
        [Variant]
        public static partial UnityUnsafeOverlapUnion Number(uint value);

        [Variant]
        public static partial UnityUnsafeOverlapUnion Mixed(uint raw, float amount, string label, Texture2D icon);

        [Variant]
        public static partial UnityUnsafeOverlapUnion Child(UnityCallbackChild child);

        [Variant]
        public static partial UnityUnsafeOverlapUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityUnsafeOverlapGeneric<T>
    {
        [Variant]
        public static partial UnityUnsafeOverlapGeneric<T> Some(T value, uint code, float weight);

        [Variant]
        public static partial UnityUnsafeOverlapGeneric<T> None();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityUnsafeOverlapParent
    {
        [Variant]
        public static partial UnityUnsafeOverlapParent Parent(UnityUnsafeOverlapUnion child, int marker);

        [Variant]
        public static partial UnityUnsafeOverlapParent Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityOverlapUnion
    {
        [Variant]
        public static partial UnityOverlapUnion Number(int value);

        [Variant]
        public static partial UnityOverlapUnion Rectangle(float width, float height);

        [Variant]
        public static partial UnityOverlapUnion Point();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityOverlapReferenceUnion
    {
        [Variant]
        public static partial UnityOverlapReferenceUnion Text(string label, Texture2D icon);

        [Variant]
        public static partial UnityOverlapReferenceUnion Number(int value);

        [Variant]
        public static partial UnityOverlapReferenceUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityOverlapParent
    {
        [Variant]
        public static partial UnityOverlapParent Parent(UnityOverlapUnion child, int marker);

        [Variant]
        public static partial UnityOverlapParent Reference(UnityOverlapReferenceUnion refChild);

        [Variant]
        public static partial UnityOverlapParent Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityCallbackParent
    {
        [Variant]
        public static partial UnityCallbackParent Parent(UnityCallbackChild child, int marker);

        [Variant]
        public static partial UnityCallbackParent Text(string label);

        [Variant]
        public static partial UnityCallbackParent Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityCollectionUnion
    {
        [Variant]
        public static partial UnityCollectionUnion Child(UnityCallbackChild child);

        [Variant]
        public static partial UnityCollectionUnion Number(float value);

        [Variant]
        public static partial UnityCollectionUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityCopyProbe
    {
        [Variant]
        public static partial UnityCopyProbe Text(int count, string label, Texture2D asset);

        [Variant]
        public static partial UnityCopyProbe Number(float value);

        [Variant]
        public static partial UnityCopyProbe Parent(UnityCallbackChild child, int marker);
    }

    [Serializable]
    public sealed class UnityAllowedCallbackClass : ISerializationCallbackReceiver
    {
        public int value;

        [NonSerialized]
        public int derived;

        public UnityAllowedCallbackClass()
        {
        }

        public UnityAllowedCallbackClass(int value)
        {
            this.value = value;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            derived = value * 2;
        }
    }

    [Serializable]
    public struct UnityAllowedCallbackStruct : ISerializationCallbackReceiver
    {
        public int value;

        [NonSerialized]
        public int derived;

        public UnityAllowedCallbackStruct(int value)
        {
            this.value = value;
            derived = 0;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            derived = value * 2;
        }
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityAllowedCallbackUnion
    {
        [Variant]
        public static partial UnityAllowedCallbackUnion Both(
            UnityAllowedCallbackStruct structValue,
            UnityAllowedCallbackClass classValue);
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityDrawerUnion
    {
        [Variant]
        public static partial UnityDrawerUnion Empty();

        [Variant]
        public static partial UnityDrawerUnion Number(float value);

        [Variant]
        public static partial UnityDrawerUnion Text(int count, string label);

        [Variant]
        public static partial UnityDrawerUnion ObjectRef(Texture2D texture);
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityGenericUnion<T>
    {
        [Variant]
        public static partial UnityGenericUnion<T> Some(T value);

        [Variant]
        public static partial UnityGenericUnion<T> None();
    }

    [Serializable]
    public sealed class UnityGenericChildHelper
    {
        public UnityCallbackChild child;
        public int marker;

        public UnityGenericChildHelper()
        {
        }

        public UnityGenericChildHelper(UnityCallbackChild child, int marker)
        {
            this.child = child;
            this.marker = marker;
        }

        public UnityCallbackChild Child
        {
            get => child;
            set => child = value;
        }
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityGenericDrawerParent
    {
        [Variant]
        public static partial UnityGenericDrawerParent Parent(UnityGenericUnion<int> child);

        [Variant]
        public static partial UnityGenericDrawerParent Empty();
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityZeroVariant
    {
    }

    [DiscriminatedUnion]
    public partial struct UnityNormalAdditive
    {
        [Variant]
        public static partial UnityNormalAdditive Number(int value);

        [Variant]
        public static partial UnityNormalAdditive Empty();
    }

    [Serializable]
    public struct UnityNestedGenericOwner<T>
    {
        public UnityGenericUnion<T> value;
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnityGenericContainerNestedUnion
    {
        [Variant]
        public static partial UnityGenericContainerNestedUnion Some(int value);

        [Variant]
        public static partial UnityGenericContainerNestedUnion Empty();
    }

    [Serializable]
    public class UnityCompositeBase
    {
        [SerializeField]
        private UnityShapeSerializable baseChild;

        public UnityShapeSerializable BaseChild => baseChild;

        protected UnityCompositeBase()
        {
        }

        protected UnityCompositeBase(UnityShapeSerializable baseChild)
        {
            this.baseChild = baseChild;
        }
    }

    [Serializable]
    public sealed class UnityCompositeDerived : UnityCompositeBase
    {
        public UnityShapeSerializable derivedChild;

        public UnityCompositeDerived()
        {
        }

        public UnityCompositeDerived(
            UnityShapeSerializable baseChild,
            UnityShapeSerializable derivedChild)
            : base(baseChild)
        {
            this.derivedChild = derivedChild;
        }
    }

    [Serializable]
    public sealed class UnityReferenceComposite
    {
        public UnityCopyProbe parent;

        [SerializeField]
        private UnityCallbackChild child;

        public UnityCallbackChild Child
        {
            get => child;
            set => child = value;
        }

        public UnityReferenceComposite()
        {
        }

        public UnityReferenceComposite(UnityCopyProbe parent, UnityCallbackChild child)
        {
            this.parent = parent;
            this.child = child;
        }
    }

}
