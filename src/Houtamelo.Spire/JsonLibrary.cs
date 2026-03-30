using System;

namespace Houtamelo.Spire;

/// <summary>
/// JSON libraries to generate converters for on a discriminated union.
/// </summary>
[Flags]
public enum JsonLibrary
{
    None = 0,
    SystemTextJson = 1,
    NewtonsoftJson = 2,
}
