namespace Houtamelo.Spire;

/// <summary>
/// Memory layout strategy for struct discriminated unions.
/// </summary>
public enum Layout
{
    /// <summary>Use the project-wide default from the Spire_DU_DefaultLayout MSBuild property. Falls back to Auto if unset.</summary>
    ReadGlobalCfg = 0,
    /// <summary>Auto-select: Overlap for non-generic, BoxedFields for generic.</summary>
    Auto = 1,
    /// <summary>Explicit layout with overlapping fields (smallest size, non-generic only).</summary>
    Overlap = 2,
    /// <summary>Each variant's fields boxed into separate object fields.</summary>
    BoxedFields = 3,
    /// <summary>Variant fields stored as a boxed ValueTuple.</summary>
    BoxedTuple = 4,
    /// <summary>All variant fields stored side-by-side (largest size, no boxing).</summary>
    Additive = 5,
    /// <summary>Unsafe overlap using Unsafe.As (requires AllowUnsafeBlocks).</summary>
    UnsafeOverlap = 6,
}
