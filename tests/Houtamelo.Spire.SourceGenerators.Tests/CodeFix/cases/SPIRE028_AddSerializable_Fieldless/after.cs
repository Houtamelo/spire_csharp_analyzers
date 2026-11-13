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
    [global::System.Serializable]
    public record struct None;
}
