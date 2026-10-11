#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityAdditiveUnion
    {
        [Variant]
        public static partial StabilityAdditiveUnion First(int amount, string label);

        [Variant]
        public static partial StabilityAdditiveUnion Empty();

        [Variant]
        public static partial StabilityAdditiveUnion Nested(StabilityNestedChildUnion child);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.BoxedFields)]
    public partial struct StabilityBoxedFieldsUnion
    {
        [Variant(FormerlySerializedAs = "OldBoxedFieldsFirst")]
        public static partial StabilityBoxedFieldsUnion First(int amount, string label);

        [Variant]
        public static partial StabilityBoxedFieldsUnion Empty();

        [Variant]
        public static partial StabilityBoxedFieldsUnion Nested(StabilityNestedChildUnion child);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.BoxedTuple)]
    public partial struct StabilityBoxedTupleUnion
    {
        [Variant(FormerlySerializedAs = "OldBoxedTupleFirst")]
        public static partial StabilityBoxedTupleUnion First(int amount, string label);

        [Variant]
        public static partial StabilityBoxedTupleUnion Empty();

        [Variant]
        public static partial StabilityBoxedTupleUnion Nested(StabilityNestedChildUnion child);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.BoxedTuple)]
    public partial struct StabilityBoxedTupleCompositeUnion
    {
        [Variant]
        public static partial StabilityBoxedTupleCompositeUnion Composite(
            StabilityNestedChildUnion child,
            int marker,
            string label);

        [Variant]
        public static partial StabilityBoxedTupleCompositeUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion(Layout.UnsafeOverlap)]
    public partial struct StabilityUnsafeOverlapUnion
    {
        [Variant(FormerlySerializedAs = "OldUnsafeOverlapFirst")]
        public static partial StabilityUnsafeOverlapUnion First(int amount, string label);

        [Variant]
        public static partial StabilityUnsafeOverlapUnion Empty();

        [Variant]
        public static partial StabilityUnsafeOverlapUnion Nested(StabilityNestedChildUnion child);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Overlap)]
    public partial struct StabilityOverlapUnion
    {
        [Variant(FormerlySerializedAs = "OldOverlapFirst")]
        public static partial StabilityOverlapUnion First(int amount, string label);

        [Variant]
        public static partial StabilityOverlapUnion Empty();

        [Variant]
        public static partial StabilityOverlapUnion Nested(StabilityNestedChildUnion child);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityNestedChildUnion
    {
        [Variant(FormerlySerializedAs = "OldNestedText")]
        public static partial StabilityNestedChildUnion Text(int count, string label);

        [Variant]
        public static partial StabilityNestedChildUnion Empty();

        [Variant]
        public static partial StabilityNestedChildUnion Number(float value);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityNestedParentUnion
    {
        [Variant]
        public static partial StabilityNestedParentUnion Child(StabilityNestedChildUnion child, int marker);

        [Variant]
        public static partial StabilityNestedParentUnion Empty();

        [Variant]
        public static partial StabilityNestedParentUnion Text(string label);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityGenericUnion<T>
    {
        [Variant(FormerlySerializedAs = "OldGenericFirst")]
        public static partial StabilityGenericUnion<T> First(T value);

        [Variant]
        public static partial StabilityGenericUnion<T> Nested(StabilityNestedChildUnion child);

        [Variant]
        public static partial StabilityGenericUnion<T> Empty();
    }

    [Serializable]
    public struct StabilityGenericOwnerInt
    {
        public StabilityGenericUnion<int> value;
    }

    [Serializable]
    public struct StabilityGenericOwnerString
    {
        public StabilityGenericUnion<string> value;
    }

    [Serializable]
    public sealed class StabilityCallbackChildHelper : ISerializationCallbackReceiver
    {
        public StabilityNestedChildUnion child;

        [NonSerialized]
        public int callbackMarker;

        public StabilityCallbackChildHelper()
        {
            child = StabilityNestedChildUnion.Text(77, "constructor-child");
        }

        public StabilityCallbackChildHelper(StabilityNestedChildUnion child)
        {
            this.child = child;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            callbackMarker = 1;
        }
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityMigrationUnion
    {
        [Variant(FormerlySerializedAs = "OldValue")]
        public static partial StabilityMigrationUnion Renamed(int value);

        [Variant(FormerlySerializedAs = "OldEmpty")]
        public static partial StabilityMigrationUnion Empty();

        [Variant]
        public static partial StabilityMigrationUnion Other(string label);
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct StabilityDrawerAliasUnion
    {
        [Variant]
        public static partial StabilityDrawerAliasUnion OldPayload(string label);

        [Variant(FormerlySerializedAs = "Old")]
        public static partial StabilityDrawerAliasUnion Current(int value);

        [Variant]
        public static partial StabilityDrawerAliasUnion Empty();
    }

    [Serializable]
    public sealed class StabilityEditorHost
    {
        public StabilityAdditiveUnion additive;
        public StabilityBoxedFieldsUnion boxedFields;
        public StabilityBoxedTupleUnion boxedTuple;
        public StabilityBoxedTupleCompositeUnion tupleComposite;
        public StabilityUnsafeOverlapUnion unsafeOverlap;
        public StabilityOverlapUnion overlap;
        public StabilityNestedParentUnion parent;
        public StabilityNestedParentUnion[] parentArray = Array.Empty<StabilityNestedParentUnion>();
        public List<StabilityNestedParentUnion> parentList = new List<StabilityNestedParentUnion>();
        public StabilityGenericOwnerInt genericInt;
        public StabilityGenericOwnerString genericString;
        public StabilityGenericUnion<int>[] genericArray = Array.Empty<StabilityGenericUnion<int>>();
        public StabilityMigrationUnion migration;
        public StabilityCallbackChildHelper callbackHelper = new StabilityCallbackChildHelper();
        public Texture2D reference = null!;
    }

}
