using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a static partial method as a variant declaration on a struct discriminated union.
/// The method name becomes the variant name; parameters become variant fields.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VariantAttribute : Attribute
{
    /// <summary>
    /// Gets or sets one previous variant name used to restore Unity serialization data.
    /// This does not add a runtime variant or affect non-Unity generation.
    /// </summary>
    public string? FormerlySerializedAs { get; set; }
}
