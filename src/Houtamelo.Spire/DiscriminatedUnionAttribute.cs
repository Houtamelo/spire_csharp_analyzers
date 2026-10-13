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
    /// <summary>The memory layout strategy for struct unions.</summary>
    public Layout Layout { get; }

    /// <summary>Whether to generate Deconstruct methods for each variant.</summary>
    public GenerateDeconstruct GenerateDeconstruct { get; }

    /// <summary>Which JSON libraries to generate converters for.</summary>
    public JsonLibrary Json { get; }

    /// <summary>The JSON property name used as the variant discriminator. Null means read from global config (default: "kind").</summary>
    public string? JsonDiscriminator { get; }

    /// <summary>Declares a union with optional storage, deconstruction, and JSON settings.</summary>
    /// <param name="layout">The struct storage strategy; ignored for record unions.</param>
    /// <param name="generateDeconstruct">Whether to generate variant deconstruction methods.</param>
    /// <param name="json">The JSON converter libraries to generate.</param>
    /// <param name="jsonDiscriminator">The JSON discriminator property name, or null to use the configured default.</param>
    public DiscriminatedUnionAttribute(
        Layout layout = Layout.ReadGlobalCfg,
        GenerateDeconstruct generateDeconstruct = GenerateDeconstruct.ReadGlobalCfg,
        JsonLibrary json = JsonLibrary.ReadGlobalCfg,
        string? jsonDiscriminator = null)
    {
        Layout = layout;
        GenerateDeconstruct = generateDeconstruct;
        Json = json;
        JsonDiscriminator = jsonDiscriminator;
    }
}
