using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

internal static class AnalyzerDescriptors
{
    public static readonly DiagnosticDescriptor SPIRE009_SwitchNotExhaustive = new(
        id: "SPIRE009",
        title: "Switch does not handle all variants of discriminated union",
        messageFormat: "Switch on '{0}' does not handle variant(s): {1}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Switch expressions and statements on discriminated union types must handle all variants explicitly.");

    public static readonly DiagnosticDescriptor SPIRE011_FieldTypeMismatch = new(
        id: "SPIRE011",
        title: "Discriminated union pattern field type mismatch",
        messageFormat: "Variant '{0}' field {1} is '{2}', not '{3}'",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE012_FieldCountMismatch = new(
        id: "SPIRE012",
        title: "Discriminated union pattern field count mismatch",
        messageFormat: "Variant '{0}' has {1} field(s), not {2}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE013_WrongVariantFieldAccess = new(
        id: "SPIRE013",
        title: "Accessing another variant's field",
        messageFormat: "'{0}' belongs to variant '{1}', not '{2}'",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE014_UnguardedFieldAccess = new(
        id: "SPIRE014",
        title: "Accessing variant field without kind guard",
        messageFormat: "Accessing variant field '{0}' without a kind guard",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
