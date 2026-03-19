using System;

namespace Spire.SourceGenerators.Model;

internal enum EmitStrategy { Record, Class, Overlap, BoxedFields, BoxedTuple }

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
    EmitStrategy Strategy,
    EquatableArray<string> TypeParameters,
    EquatableArray<VariantInfo> Variants,
    /// Containing types from outermost to innermost (empty if top-level).
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    /// Optional diagnostic to report (e.g., Layout on record, Overlap on generic).
    /// If IsError, no source is emitted for this union.
    UnionDiagnostic? Diagnostic
) : IEquatable<UnionDeclaration>;
