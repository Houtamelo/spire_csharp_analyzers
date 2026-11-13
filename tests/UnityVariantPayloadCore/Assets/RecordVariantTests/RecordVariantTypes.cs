#nullable enable

using System;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityRecordVariantTests;

[Serializable]
public class RecordNode
{
    public int id;
    public string label = string.Empty;
}

[Serializable]
public sealed class DerivedRecordNode : RecordNode
{
    public int derivedValue;
}

[Serializable]
[DiscriminatedUnion]
public partial struct RecordVariantUnion
{
    [Variant(FormerlySerializedAs = "OldSlider")]
    public partial record struct Slider(int value)
    {
        public int Extra { get; set; } = default;
    }

    [Variant]
    [Serializable]
    public record struct FullyAnnotated(
        [field: SerializeField] int value);

    [Variant(FormerlySerializedAs = "OldExplicit")]
    [Serializable]
    public record struct Explicit(int value)
    {
        [SerializeField]
        public int value = value;
    }

    [Variant]
    public static partial RecordVariantUnion Method(int value);

    [Variant]
    public partial record struct Empty;

    [Variant]
    public partial record struct Managed(
        [UnitySerializeReference] RecordNode? node);
}
