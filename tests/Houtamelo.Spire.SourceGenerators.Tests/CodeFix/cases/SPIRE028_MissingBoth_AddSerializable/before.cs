//@ unity
//@ codefix: Spire.RecordVariant.AddSerializable
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Variant]
    public record struct Value(int amount);
}
