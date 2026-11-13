//@ unity
//@ codefix: Spire.RecordVariant.AddFieldAttributes
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Serializable, Variant]
    public record struct Value(int value)
    {
        [global::UnityEngine.SerializeField]
        public int value = value;
    }
}
