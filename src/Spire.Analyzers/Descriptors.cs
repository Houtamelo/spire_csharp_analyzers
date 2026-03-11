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
}
