using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators;

internal static class Diagnostics
{
    private static readonly DiagnosticDescriptor NestedTypeNotSupported = new(
        id: "SPIRE_DU001",
        title: "Nested type not supported for [DiscriminatedUnion]",
        messageFormat: "Nested type declarations are not supported for [DiscriminatedUnion]",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RefStructNotSupported = new(
        id: "SPIRE_DU002",
        title: "ref struct not supported for [DiscriminatedUnion]",
        messageFormat: "ref struct is not supported for [DiscriminatedUnion]",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoVariantsFound = new(
        id: "SPIRE_DU003",
        title: "No [Variant] methods found",
        messageFormat: "No [Variant] methods found on discriminated union type",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly Dictionary<string, DiagnosticDescriptor> DescriptorMap = new()
    {
        ["SPIRE_DU001"] = NestedTypeNotSupported,
        ["SPIRE_DU002"] = RefStructNotSupported,
        ["SPIRE_DU003"] = NoVariantsFound,
    };

    public static DiagnosticDescriptor GetDescriptor(UnionDiagnostic diag)
    {
        if (DescriptorMap.TryGetValue(diag.Id, out var descriptor))
            return descriptor;

        // Fallback for unknown IDs (shouldn't happen, but safe)
        return new DiagnosticDescriptor(
            diag.Id,
            diag.Message,
            diag.Message,
            "SourceGeneration",
            diag.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
