namespace Houtamelo.Spire;

/// <summary>
/// Controls whether Deconstruct methods are generated for each variant of a discriminated union.
/// </summary>
public enum GenerateDeconstruct
{
    /// <summary>Use the project-wide default from the Spire_DU_DefaultGenerateDeconstruct MSBuild property. Falls back to Yes if unset.</summary>
    ReadGlobalCfg = 0,
    /// <summary>Generate variant deconstruction methods.</summary>
    Yes = 1,
    /// <summary>Do not generate variant deconstruction methods.</summary>
    No = 2,
}
