#if UNITY_EDITOR
#nullable enable

using System;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    public enum UnityDrawerNonOrdinal
    {
        Small = 3,
        Large = 17,
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnityDrawerNumericUnion
    {
        [Variant]
        public static partial UnityDrawerNumericUnion Values(
            long longValue,
            double doubleValue,
            UnityDrawerNonOrdinal enumValue);
    }

    public enum UnityDrawerInspectorNamedEnum
    {
        [InspectorName("Tiny choice")]
        Small = 3,

        [InspectorName("Huge choice")]
        Large = 17,
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnityDrawerInspectorNamedEnumUnion
    {
        [Variant]
        public static partial UnityDrawerInspectorNamedEnumUnion Values(
            long longValue,
            double doubleValue,
            UnityDrawerInspectorNamedEnum enumValue);
    }

    [Flags]
    public enum UnityDrawerNonOrdinalFlags
    {
        None = 0,

        [InspectorName("First Flag")]
        First = 2,

        [InspectorName("Second Flag")]
        Second = 8,

        [InspectorName("Third Flag")]
        Third = 32,
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnityDrawerFlagsUnion
    {
        [Variant]
        public static partial UnityDrawerFlagsUnion Values(
            long longValue,
            double doubleValue,
            UnityDrawerNonOrdinalFlags flagsValue);
    }
}
#endif
