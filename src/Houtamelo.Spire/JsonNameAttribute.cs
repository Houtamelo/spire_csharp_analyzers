using System;

namespace Houtamelo.Spire;

/// <summary>
/// Overrides the JSON name for a variant, variant field, or the union type itself.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Parameter,
    Inherited = false)]
public sealed class JsonNameAttribute : Attribute
{
    /// <summary>The custom JSON name.</summary>
    public string Name { get; }

    /// <summary>Overrides the generated JSON name.</summary>
    /// <param name="name">The name written to and read from JSON.</param>
    public JsonNameAttribute(string name) => Name = name;
}
