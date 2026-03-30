using System;

namespace Houtamelo.Spire;

/// <summary>
/// JSON libraries to generate converters for on a discriminated union.
/// </summary>
[Flags]
public enum JsonLibrary
{
    /// <summary>Use the project-wide default from the Spire_DU_DefaultJson MSBuild property. Falls back to None if unset.</summary>
    ReadGlobalCfg = -1,
    None = 0,
    SystemTextJson = 1,
    NewtonsoftJson = 2,
}
