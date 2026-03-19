using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators;

internal static class Diagnostics
{
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

    private static readonly DiagnosticDescriptor LayoutIgnoredForRecordClass = new(
        id: "SPIRE_DU004",
        title: "Layout parameter ignored for record/class discriminated unions",
        messageFormat: "Layout parameter is ignored for record/class discriminated unions",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OverlapOnGenericStruct = new(
        id: "SPIRE_DU005",
        title: "Generic structs cannot use Overlap layout",
        messageFormat: "Generic structs cannot use Overlap layout (CLR restriction); use BoxedFields or BoxedTuple",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly Dictionary<string, DiagnosticDescriptor> DescriptorMap = new()
    {
        ["SPIRE_DU002"] = RefStructNotSupported,
        ["SPIRE_DU003"] = NoVariantsFound,
        ["SPIRE_DU004"] = LayoutIgnoredForRecordClass,
        ["SPIRE_DU005"] = OverlapOnGenericStruct,
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
