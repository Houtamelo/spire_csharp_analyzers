using System;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers;

/// Parsed project-wide Spire configuration from MSBuild properties.
/// Used by both the source generator (DU defaults) and analyzers (enum enforcement).
internal readonly struct SpireGlobalConfig : IEquatable<SpireGlobalConfig>
{
    public ReferenceStorage DefaultReferenceStorage { get; }
    public JsonLibrary DefaultJson { get; }
    public string DefaultJsonDiscriminator { get; }
    public bool EnforceExhaustivenessOnAllEnumTypes { get; }

    public SpireGlobalConfig(
        ReferenceStorage defaultReferenceStorage,
        JsonLibrary defaultJson,
        string defaultJsonDiscriminator,
        bool enforceExhaustivenessOnAllEnumTypes)
    {
        DefaultReferenceStorage = defaultReferenceStorage;
        DefaultJson = defaultJson;
        DefaultJsonDiscriminator = defaultJsonDiscriminator;
        EnforceExhaustivenessOnAllEnumTypes = enforceExhaustivenessOnAllEnumTypes;
    }

    public static SpireGlobalConfig Parse(AnalyzerConfigOptions globalOptions)
    {
        var referenceStorage = ReferenceStorage.Auto;
        if (globalOptions.TryGetValue("build_property.Spire_DU_DefaultReferenceStorage", out var storageStr))
        {
            referenceStorage = storageStr switch
            {
                "Typed" => ReferenceStorage.Typed,
                "Shared" => ReferenceStorage.Shared,
                _ => ReferenceStorage.Auto,
            };
        }

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
            referenceStorage, json, jsonDiscriminator, enforceExhaustiveness);
    }

    public bool Equals(SpireGlobalConfig other) =>
        DefaultReferenceStorage == other.DefaultReferenceStorage
        && DefaultJson == other.DefaultJson
        && string.Equals(DefaultJsonDiscriminator, other.DefaultJsonDiscriminator, StringComparison.Ordinal)
        && EnforceExhaustivenessOnAllEnumTypes == other.EnforceExhaustivenessOnAllEnumTypes;

    public override bool Equals(object? obj) => obj is SpireGlobalConfig other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)DefaultReferenceStorage * 397;
            hash = (hash * 397) ^ (int)DefaultJson;
            hash = (hash * 397) ^ DefaultJsonDiscriminator.GetHashCode();
            hash = (hash * 397) ^ EnforceExhaustivenessOnAllEnumTypes.GetHashCode();
            return hash;
        }
    }
}
