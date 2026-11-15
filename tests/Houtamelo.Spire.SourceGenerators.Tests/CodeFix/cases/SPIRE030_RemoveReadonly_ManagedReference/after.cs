//@ unity
//@ codefix: Spire.RecordVariant.RemoveReadonly
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;

[Serializable]
public sealed class Node
{
    public int id;
}

[Serializable, DiscriminatedUnion]
public partial struct Customization
{
    [Variant]
    [Serializable]
    public partial record struct Slider([field: SerializeReference] Node? node);
}
