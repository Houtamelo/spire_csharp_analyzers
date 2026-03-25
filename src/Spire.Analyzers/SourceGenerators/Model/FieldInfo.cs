using System;

namespace Spire.SourceGenerators.Model;

/// Region classification for Overlap layout field placement.
internal enum FieldRegion { Unmanaged, Reference, Boxed }

internal sealed record FieldInfo(
    string Name,
    string TypeFullName,
    bool IsUnmanaged,
    bool IsReferenceType,
    /// Computed sizeof at parse time (null if not computable).
    /// Populated from ITypeSymbol while the semantic model is available.
    /// Used by FieldClassifier for Overlap region offset computation.
    int? KnownSize,
    /// [JsonName] override for JSON serialization. Null = use Name.
    string? JsonName
) : IEquatable<FieldInfo>;
