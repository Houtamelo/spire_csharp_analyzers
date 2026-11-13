using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a static partial method or nested record struct as a variant declaration
/// on a discriminated union. A method's parameters, or a record struct's
/// positional parameters, become variant fields.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
public sealed class VariantAttribute : Attribute
{
    /// <summary>
    /// Gets or sets one previous variant name used to restore Unity serialization data.
    /// This does not add a runtime variant or affect non-Unity generation.
    /// </summary>
    public string? FormerlySerializedAs { get; set; }
}
