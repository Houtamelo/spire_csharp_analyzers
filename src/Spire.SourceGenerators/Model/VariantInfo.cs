using System;

namespace Spire.SourceGenerators.Model;

internal sealed record VariantInfo(
    string Name,
    EquatableArray<FieldInfo> Fields,
    /// [JsonName] override for JSON serialization. Null = use Name.
    string? JsonName,
    /// Explicit accessibility keyword from source. Empty if implicit.
    string AccessibilityKeyword
) : IEquatable<VariantInfo>;
