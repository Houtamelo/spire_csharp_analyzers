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
    public record struct Value(int @event);
}
