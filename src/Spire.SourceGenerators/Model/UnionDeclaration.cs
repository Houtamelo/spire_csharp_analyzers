using System;

namespace Spire.SourceGenerators.Model;

internal enum EmitStrategy { Record, Class, Overlap, BoxedFields, BoxedTuple, Additive, UnsafeOverlap }

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
    string Name
) : IEquatable<ContainingTypeInfo>;

internal sealed record UnionDeclaration(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string DeclarationKeyword,
    bool IsReadonly,
    bool IsRefStruct,
    EmitStrategy Strategy,
    bool GenerateDeconstruct,
    EquatableArray<string> TypeParameters,
    EquatableArray<VariantInfo> Variants,
    /// Containing types from outermost to innermost (empty if top-level).
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    /// Optional diagnostic to report (e.g., Layout on record, Overlap on generic).
    /// If IsError, no source is emitted for this union.
    UnionDiagnostic? Diagnostic,
    /// Which JSON libraries to generate converters for.
    JsonLibrary Json,
    /// Discriminator property name in JSON (default "kind").
    string JsonDiscriminator
) : IEquatable<UnionDeclaration>;
