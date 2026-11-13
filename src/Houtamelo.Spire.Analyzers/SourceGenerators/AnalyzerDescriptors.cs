using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

internal static class AnalyzerDescriptors
{
    public static readonly DiagnosticDescriptor SPIRE009_SwitchNotExhaustive = new(
        id: "SPIRE009",
        title: "Switch does not handle all possible cases of type marked with `[EnforceExhaustiveness]`",
        messageFormat: "Switch on '{0}' does not handle case(s): {1}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Switch expressions and statements on discriminated unions and types marked with [EnforceExhaustiveness] must handle all possible cases explicitly.");

    public static readonly DiagnosticDescriptor SPIRE009_InterfaceSwitchNotExhaustive = new(
        id: "SPIRE009",
        title: "Switch does not handle all possible cases of interface marked with `[EnforceExhaustiveness]`",
        messageFormat: "Switch on '{0}' does not handle case(s): {1}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Switch expressions and statements on discriminated unions and types marked with [EnforceExhaustiveness] must handle all possible cases explicitly.");

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

    // Keep the actionable help on separate lines in the user-facing diagnostic.
#pragma warning disable RS1032
    public static readonly DiagnosticDescriptor SPIRE013_WrongVariantFieldAccess = new(
        id: "SPIRE013",
        title: "Attempting to access mismatched variant data",
        messageFormat:
            "The field '{0}' only contains valid data when 'kind={1}', " +
            "but within this scope, it is statically known that {2}.\n" +
            "Help: {3}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE014_UnguardedFieldAccess = new(
        id: "SPIRE014",
        title: "Attempting to access variant data without checking 'kind'",
        messageFormat:
            "The field '{0}' only contains valid data when 'kind={1}', " +
            "but within this scope, the value of 'kind' is unknown.\n" +
            "Help 1: Check if 'kind={1}' before accessing the field '{0}'.\n" +
            "Help 2: If you are absolutely sure that 'kind={1}', you can suppress " +
            "this diagnostic with `#pragma warning disable SPIRE014`.",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
#pragma warning restore RS1032

    public static readonly DiagnosticDescriptor SPIRE028_RecordVariantSerialization = new(
        id: "SPIRE028",
        title: "Record variant needs serialization attributes",
        messageFormat: "Variant '{0}' needs serialization attributes for serializable union '{1}': {2}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Serializable unions require serializable variant payloads. Add the missing type and positional backing-field attributes, or make the record partial so Spire can supply them.");

    public static readonly DiagnosticDescriptor SPIRE029_RecordVariantReconstruction = new(
        id: "SPIRE029",
        title: "Authored positional member participates in payload reconstruction",
        messageFormat: "Variant '{0}' customizes positional member '{1}'. Union conversion copies its positional values and payload access reconstructs the record; custom initialization or getter behavior can change the reconstructed values.",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
