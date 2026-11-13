//@ unity
//@ codefix: Spire.RecordVariant.AddFieldAttributes
#nullable enable
using System;
using UnityEngine;
using Houtamelo.Spire;
using SR = UnityEngine.SerializeReference;

[Serializable]
public class Node { public int id; }

[Serializable, DiscriminatedUnion]
public partial struct U
{
    [Variant]
    [Serializable]
    public record struct Value(
        [field: SR] Node? node,
        int amount);
}
