using System;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal sealed record VariantInfo(
    string Name,
    EquatableArray<FieldInfo> Fields,
    /// [JsonName] override for JSON serialization. Null = use Name.
    string? JsonName,
    /// Explicit accessibility keyword from source. Empty if implicit.
    string AccessibilityKeyword,
    /// One previous variant name used by Unity serialization. Null = no alias.
    string? FormerlySerializedAs = null,
    /// False when Unity alias validation rejected the former name. The original
    /// value remains available for diagnostics, but emitters must not generate
    /// alias-only declarations or restore branches for it.
    bool FormerlySerializedAsIsValid = true,
    /// True when the canonical payload is an authored nested record struct.
    bool IsRecordStruct = false,
    /// True when the authored record declaration is partial.
    bool IsPartial = false,
    /// Names of positional properties explicitly declared by the user.
    EquatableArray<string> AuthoredPropertyNames = default,
    /// True when the authored record already carries SerializableAttribute.
    bool HasSerializableAttribute = false,
    /// True when the authored record is readonly.
    bool IsReadonly = false
) : IEquatable<VariantInfo>
{
    public bool HasFormerlySerializedAs
        => FormerlySerializedAsIsValid &&
            !string.IsNullOrEmpty(FormerlySerializedAs) &&
            !string.Equals(FormerlySerializedAs, Name, StringComparison.Ordinal);
}
