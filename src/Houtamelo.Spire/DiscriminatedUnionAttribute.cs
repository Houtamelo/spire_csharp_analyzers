using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a struct or record as a discriminated union. The Spire source generator
/// will emit variant storage, pattern matching support, and optional JSON serialization.
/// </summary>
[AttributeUsage(
    AttributeTargets.Struct | AttributeTargets.Class,
    Inherited = false)]
public sealed class DiscriminatedUnionAttribute : Attribute
{
    /// <summary>The memory layout strategy for struct unions.</summary>
    public Layout Layout { get; }

    /// <summary>Whether to generate Deconstruct methods for each variant. Default: true.</summary>
    public bool GenerateDeconstruct { get; set; } = true;

    /// <summary>Which JSON libraries to generate converters for. Default: None.</summary>
    public JsonLibrary Json { get; set; } = JsonLibrary.None;

    /// <summary>The JSON property name used as the variant discriminator. Default: "kind".</summary>
    public string JsonDiscriminator { get; set; } = "kind";

    public DiscriminatedUnionAttribute(Layout layout = Layout.Auto)
        => Layout = layout;
}
