using System;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal sealed record FieldInfo(
    string Name,
    string TypeFullName,
    bool IsUnmanaged,
    bool IsReferenceType,
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
    bool RequiresUnityRuntimeDispatch = false,
    /// True when the variant parameter explicitly requests Unity managed-reference serialization.
    bool IsUnityManagedReference = false,
    /// Constructor-only CLR-equivalent type identity used for grouping.
    /// This omits reference nullability and tuple element names.
    string? ConstructorTypeKey = null,
    /// Generic-dependent values cannot enter nongeneric explicit-layout helpers.
    bool IsGenericDependent = false,
    /// Reused typed storage accepts the merged nullable annotations of all users.
    string? StorageTypeFullName = null
) : IEquatable<FieldInfo>;
