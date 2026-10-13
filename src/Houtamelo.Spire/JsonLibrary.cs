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
    /// <summary>Do not generate JSON converters.</summary>
    None = 0,
    /// <summary>Generate a System.Text.Json converter.</summary>
    SystemTextJson = 1,
    /// <summary>Generate a Newtonsoft.Json converter.</summary>
    NewtonsoftJson = 2,
}
