//@ unity
//@ codefix: Spire.RecordVariant.AddFieldAttributes
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;
using MR = Houtamelo.Spire.UnitySerializeReferenceAttribute;

[Serializable]
public class Node { public int id; }

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Variant]
    [Serializable]
    public record struct Value([MR] Node? node);
}
