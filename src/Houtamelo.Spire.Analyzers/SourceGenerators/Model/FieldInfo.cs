using System;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

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
    string? JsonName,
    /// True when the field type is another Unity-serializable union.
    bool IsUnityUnion = false,
    /// True when this field is a one-dimensional array or List whose element is
    /// another Unity-serializable union.
    bool IsUnityUnionCollection = false,
    /// True when a serializable composite field contains a generated Unity union
    /// below its serialized fields.
    bool IsUnityComposite = false,
    /// True when the field shape contains an unresolved generic type parameter.
    /// Closed runtime values are dispatched through the managed Unity bridge.
    bool RequiresUnityRuntimeDispatch = false
) : IEquatable<FieldInfo>;
