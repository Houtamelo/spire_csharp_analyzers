using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a struct or record as a discriminated union. The Spire source generator
/// will emit variant storage, pattern matching support, and optional JSON serialization.
/// </summary>
[AttributeUsage(
    AttributeTargets.Struct | AttributeTargets.Class,
    Inherited = false)]
public sealed class DiscriminatedUnionAttribute : EnforceExhaustivenessAttribute
{
    /// <summary>Reference storage policy, or ReadGlobalCfg to use the project default.</summary>
    public ReferenceStorage ReferenceStorage { get; set; } = ReferenceStorage.ReadGlobalCfg;

    /// <summary>Which JSON libraries to generate converters for.</summary>
    public JsonLibrary Json { get; }

    /// <summary>The JSON property name used as the variant discriminator. Null means read from global config (default: "kind").</summary>
    public string? JsonDiscriminator { get; }

    /// <summary>Declares a union with optional JSON settings.</summary>
    /// <param name="json">The JSON converter libraries to generate.</param>
    /// <param name="jsonDiscriminator">The JSON discriminator property name, or null to use the configured default.</param>
    public DiscriminatedUnionAttribute(
        JsonLibrary json = JsonLibrary.ReadGlobalCfg,
        string? jsonDiscriminator = null)
    {
        Json = json;
        JsonDiscriminator = jsonDiscriminator;
    }
}
