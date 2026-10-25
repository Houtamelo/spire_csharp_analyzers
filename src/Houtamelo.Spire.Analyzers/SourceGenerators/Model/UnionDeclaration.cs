using System;
using System.Collections.Immutable;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal enum EmitStrategy { Record, VariantPayload }

internal enum ReferenceStorage { ReadGlobalCfg, Auto, Typed, Shared }

[Flags]
internal enum JsonLibrary
{
    None = 0,
    SystemTextJson = 1,
    NewtonsoftJson = 2,
}

/// Carries a diagnostic ID + message for the generator to report.
/// Simpler than passing Roslyn DiagnosticDescriptor through the equatable model.
/// Location fields allow reconstructing a Location on the reporting side.
internal sealed record UnionDiagnostic(
    string Id,
    string Message,
    bool IsError,
    string FilePath,
    int StartOffset,
    int Length,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn
) : IEquatable<UnionDiagnostic>;

/// A containing type in the nesting chain (outermost first).
internal sealed record ContainingTypeInfo(
    string AccessibilityKeyword,
    string Keyword,
    string Name,
    EquatableArray<string> TypeParameters
) : IEquatable<ContainingTypeInfo>;

/// Raw attribute values before project-default resolution.
internal sealed record RawAttributeConfig(
    ReferenceStorage ReferenceStorage,
    int Json,
    string? JsonDiscriminator
) : IEquatable<RawAttributeConfig>
{
    public static readonly RawAttributeConfig AllSentinels = new(ReferenceStorage.ReadGlobalCfg, -1, null);
}

internal sealed record UnionDeclaration(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string DeclarationKeyword,
    bool IsReadonly,
    bool IsRefStruct,
    EmitStrategy Strategy,
    EquatableArray<string> TypeParameters,
    EquatableArray<VariantInfo> Variants,
    /// Containing types from outermost to innermost (empty if top-level).
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    /// A declaration diagnostic. Reporting an error does not suppress generated API.
    UnionDiagnostic? Diagnostic,
    /// Which JSON libraries to generate converters for.
    JsonLibrary Json,
    /// Discriminator property name in JSON (default "kind").
    string JsonDiscriminator,
    /// Whether System.Runtime.CompilerServices.IsExternalInit is available (C# 9+ init support).
    bool HasInitProperties,
    /// Raw attribute values for global config resolution.
    RawAttributeConfig RawConfig,
    /// Whether the union type has type parameters.
    bool IsGeneric,
    /// Whether the complete union symbol has System.SerializableAttribute.
    bool IsUnitySerializable = false
) : IEquatable<UnionDeclaration>
{
    public ReferenceStorage ReferenceStorage { get; init; } = ReferenceStorage.Auto;

    /// Engine-dependent output requires supported declaration shape and references,
    /// independently of any diagnostic reported for the declaration.
    public bool EmitUnitySerialization { get; init; }

    public EquatableArray<string> TypeParameterConstraints { get; init; } =
        new(ImmutableArray<string>.Empty);
}
