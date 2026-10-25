namespace Houtamelo.Spire;

/// <summary>Controls storage reuse for references and structs containing references.</summary>
public enum ReferenceStorage
{
    /// <summary>Use the project default, falling back to Auto.</summary>
    ReadGlobalCfg,
    /// <summary>Share object slots when their count is at most half the typed slot count.</summary>
    Auto,
    /// <summary>Reuse compatible typed slots across variants without boxing.</summary>
    Typed,
    /// <summary>Reuse object slots across variants, boxing structs containing references.</summary>
    Shared,
}
