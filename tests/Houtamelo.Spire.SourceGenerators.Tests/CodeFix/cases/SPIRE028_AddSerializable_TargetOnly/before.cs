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
    public record struct NeedsType([field: SerializeField] int amount);

    [Variant]
    [Serializable]
    public record struct Ready([field: SerializeField] int amount);
}
