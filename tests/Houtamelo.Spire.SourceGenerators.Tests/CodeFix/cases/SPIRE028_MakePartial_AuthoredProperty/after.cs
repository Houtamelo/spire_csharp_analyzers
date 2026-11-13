//@ unity
//@ codefix: Spire.RecordVariant.MakePartial
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Variant]
    [Serializable]
    public partial record struct Value(int amount)
    {
        public int amount { get; set; } = amount + 1;
    }
}
