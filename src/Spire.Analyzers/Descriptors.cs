using Microsoft.CodeAnalysis;

namespace Spire.Analyzers;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor SPIRE001_ArrayOfMustBeInitStruct = new(
        id: "SPIRE001",
        title: "Non-empty array of [MustBeInit] struct produces default instances",
        messageFormat: "Non-empty array of struct '{0}' marked with [MustBeInit] will contain default (uninitialized) instances",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Creating a non-empty array of a struct marked with [MustBeInit] fills all elements with default(T), "
                   + "bypassing any required initialization. Use an empty array or provide an explicit initializer.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE001.md"
    );

    public static readonly DiagnosticDescriptor SPIRE002_MustBeInitOnFieldlessType = new(
        id: "SPIRE002",
        title: "[MustBeInit] on fieldless type has no effect",
        messageFormat: "[MustBeInit] on type '{0}' has no effect because it has no instance fields",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [MustBeInit] attribute marks types whose default value is considered uninitialized. "
                   + "A type with no instance fields has only one possible value (the default), so the attribute serves no purpose. "
                   + "Note that auto-properties generate backing fields and do count; non-auto (computed) properties do not.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE002.md"
    );
}
