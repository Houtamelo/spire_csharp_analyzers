namespace Houtamelo.Spire;

/// <summary>
/// Memory layout strategy for struct discriminated unions.
/// </summary>
public enum Layout
{
    /// <summary>Auto-select: Overlap for non-generic, BoxedFields for generic.</summary>
    Auto,
    /// <summary>Explicit layout with overlapping fields (smallest size, non-generic only).</summary>
    Overlap,
    /// <summary>Each variant's fields boxed into separate object fields.</summary>
    BoxedFields,
    /// <summary>Variant fields stored as a boxed ValueTuple.</summary>
    BoxedTuple,
    /// <summary>All variant fields stored side-by-side (largest size, no boxing).</summary>
    Additive,
    /// <summary>Unsafe overlap using Unsafe.As (requires AllowUnsafeBlocks).</summary>
    UnsafeOverlap,
}
