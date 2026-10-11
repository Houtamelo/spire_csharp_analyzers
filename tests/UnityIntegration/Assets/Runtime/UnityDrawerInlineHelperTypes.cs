#nullable enable

using System;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    [Serializable]
    public sealed class UnityDrawerInlineHelper
    {
        public string label = string.Empty;
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnityDrawerInlineHelperUnion
    {
        [Variant]
        public static partial UnityDrawerInlineHelperUnion Helper(UnityDrawerInlineHelper helper);

        [Variant]
        public static partial UnityDrawerInlineHelperUnion Empty();
    }

}
