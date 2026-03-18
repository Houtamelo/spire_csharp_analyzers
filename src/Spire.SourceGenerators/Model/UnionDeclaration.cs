using System;

namespace Spire.SourceGenerators.Model;

internal enum EmitStrategy { Record, Class, Overlap, BoxedFields, BoxedTuple }

/// Carries a diagnostic ID + message for the generator to report.
/// Simpler than passing Roslyn DiagnosticDescriptor through the equatable model.
internal sealed record UnionDiagnostic(
    string Id,
    string Message,
    bool IsError
) : IEquatable<UnionDiagnostic>;

internal sealed record UnionDeclaration(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string DeclarationKeyword,
    bool IsReadonly,
    EmitStrategy Strategy,
    EquatableArray<string> TypeParameters,
    EquatableArray<VariantInfo> Variants,
    /// Optional diagnostic to report (e.g., Layout on record, Overlap on generic).
    /// If IsError, no source is emitted for this union.
    UnionDiagnostic? Diagnostic
) : IEquatable<UnionDeclaration>;
