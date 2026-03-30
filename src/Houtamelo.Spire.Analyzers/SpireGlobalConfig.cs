using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers;

/// Parsed project-wide Spire configuration from MSBuild properties.
/// Used by both the source generator (DU defaults) and analyzers (enum enforcement).
internal readonly struct SpireGlobalConfig : IEquatable<SpireGlobalConfig>
{
    /// Default Layout as the public enum int value (1=Auto, 2=Overlap, ...). 0=ReadGlobalCfg never stored here.
    public int DefaultLayout { get; }
    public bool DefaultGenerateDeconstruct { get; }
    public JsonLibrary DefaultJson { get; }
    public string DefaultJsonDiscriminator { get; }
    public bool EnforceExhaustivenessOnAllEnumTypes { get; }

    public SpireGlobalConfig(
        int defaultLayout,
        bool defaultGenerateDeconstruct,
        JsonLibrary defaultJson,
        string defaultJsonDiscriminator,
        bool enforceExhaustivenessOnAllEnumTypes)
    {
        DefaultLayout = defaultLayout;
        DefaultGenerateDeconstruct = defaultGenerateDeconstruct;
        DefaultJson = defaultJson;
        DefaultJsonDiscriminator = defaultJsonDiscriminator;
        EnforceExhaustivenessOnAllEnumTypes = enforceExhaustivenessOnAllEnumTypes;
    }

    public static SpireGlobalConfig Parse(AnalyzerConfigOptions globalOptions)
    {
        var layout = 1; // Auto
        if (globalOptions.TryGetValue("build_property.Spire_DU_DefaultLayout", out var layoutStr))
        {
            layout = layoutStr switch
            {
                "Auto" => 1,
                "Overlap" => 2,
                "BoxedFields" => 3,
                "BoxedTuple" => 4,
                "Additive" => 5,
                "UnsafeOverlap" => 6,
                _ => 1,
            };
        }

        var generateDeconstruct = true;
        if (globalOptions.TryGetValue("build_property.Spire_DU_DefaultGenerateDeconstruct", out var gdStr))
            generateDeconstruct = !string.Equals(gdStr, "false", StringComparison.OrdinalIgnoreCase);

        var json = JsonLibrary.None;
        if (globalOptions.TryGetValue("build_property.Spire_DU_DefaultJson", out var jsonStr))
        {
            json = jsonStr switch
            {
                "None" => JsonLibrary.None,
                "SystemTextJson" => JsonLibrary.SystemTextJson,
                "NewtonsoftJson" => JsonLibrary.NewtonsoftJson,
                "Both" => JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson,
                _ => JsonLibrary.None,
            };
        }

        var jsonDiscriminator = "kind";
        if (globalOptions.TryGetValue("build_property.Spire_DU_DefaultJsonDiscriminator", out var discStr)
            && !string.IsNullOrEmpty(discStr))
        {
            jsonDiscriminator = discStr;
        }

        var enforceExhaustiveness = false;
        if (globalOptions.TryGetValue("build_property.Spire_EnforceExhaustivenessOnAllEnumTypes", out var eeStr))
            enforceExhaustiveness = string.Equals(eeStr, "true", StringComparison.OrdinalIgnoreCase);

        return new SpireGlobalConfig(
            layout, generateDeconstruct, json, jsonDiscriminator, enforceExhaustiveness);
    }

    public bool Equals(SpireGlobalConfig other) =>
        DefaultLayout == other.DefaultLayout
        && DefaultGenerateDeconstruct == other.DefaultGenerateDeconstruct
        && DefaultJson == other.DefaultJson
        && string.Equals(DefaultJsonDiscriminator, other.DefaultJsonDiscriminator, StringComparison.Ordinal)
        && EnforceExhaustivenessOnAllEnumTypes == other.EnforceExhaustivenessOnAllEnumTypes;

    public override bool Equals(object? obj) => obj is SpireGlobalConfig other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = DefaultLayout * 397;
            hash = (hash * 397) ^ DefaultGenerateDeconstruct.GetHashCode();
            hash = (hash * 397) ^ (int)DefaultJson;
            hash = (hash * 397) ^ DefaultJsonDiscriminator.GetHashCode();
            hash = (hash * 397) ^ EnforceExhaustivenessOnAllEnumTypes.GetHashCode();
            return hash;
        }
    }
}
