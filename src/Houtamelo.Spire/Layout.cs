namespace Houtamelo.Spire;

/// <summary>
/// Memory layout strategy for struct discriminated unions.
/// </summary>
public enum Layout
{
    /// <summary>Use the project-wide default from the Spire_DU_DefaultLayout MSBuild property. Falls back to Auto if unset.</summary>
    ReadGlobalCfg = 0,
    /// <summary>Auto-select: Additive for Unity-serializable unions; otherwise Overlap for non-generic structs and BoxedFields for generic structs.</summary>
    Auto = 1,
    /// <summary>Explicit layout with compatible same-name fields sharing offsets. Non-generic only.</summary>
    Overlap = 2,
    /// <summary>Object slots for resolved field names; value-type fields are boxed individually.</summary>
    BoxedFields = 3,
    /// <summary>Variant fields stored as a boxed ValueTuple.</summary>
    BoxedTuple = 4,
    /// <summary>Typed fields stored side by side, with compatible same-name fields sharing slots and no value-type boxing.</summary>
    Additive = 5,
    /// <summary>Unmanaged fields in a raw buffer accessed through unaligned Unsafe reads/writes, with managed/generic fields in separate slots. Requires AllowUnsafeBlocks.</summary>
    UnsafeOverlap = 6,
}
