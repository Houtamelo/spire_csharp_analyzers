//@ unity
//@ codefix: Spire.RecordVariant.AddFieldAttributes
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Variant]
    [Serializable]
    public partial record struct Value(int value);

    public partial record struct Value
    {
        [field: global::UnityEngine.SerializeField]
        public int value { get; set; } = value;
    }
}
