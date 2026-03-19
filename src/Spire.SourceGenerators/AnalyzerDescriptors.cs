using Microsoft.CodeAnalysis;

namespace Spire.SourceGenerators;

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

    public static readonly DiagnosticDescriptor SPIRE010_WildcardInsteadOfExhaustive = new(
        id: "SPIRE010",
        title: "Switch uses wildcard instead of exhaustive variant matching",
        messageFormat: "Switch on '{0}' uses wildcard instead of exhaustive variant matching; missing: {1}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Switch covers all variants via a wildcard/discard arm instead of explicit variant matching.");
}
