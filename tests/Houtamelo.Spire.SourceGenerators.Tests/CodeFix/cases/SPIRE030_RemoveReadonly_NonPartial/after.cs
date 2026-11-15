//@ unity
//@ codefix: Spire.RecordVariant.RemoveReadonly
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable, DiscriminatedUnion]
public partial struct Customization
{
    [Variant]
    public record struct Slider(int amount);
}
