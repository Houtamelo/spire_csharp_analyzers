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

    private static readonly DiagnosticDescriptor LayoutIgnoredForRecord = new(
        id: "SPIRE_DU004",
        title: "Layout parameter ignored for record discriminated unions",
        messageFormat: "Layout parameter is ignored for record discriminated unions",
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

    private static readonly DiagnosticDescriptor SystemTextJsonNotReferenced = new(
        id: "SPIRE_DU006",
        title: "System.Text.Json not referenced",
        messageFormat: "System.Text.Json not referenced but Json includes SystemTextJson",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NewtonsoftJsonNotReferenced = new(
        id: "SPIRE_DU007",
        title: "Newtonsoft.Json not referenced",
        messageFormat: "Newtonsoft.Json not referenced but Json includes NewtonsoftJson",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RefStructJsonNotSupported = new(
        id: "SPIRE_DU008",
        title: "ref struct cannot use JSON generation",
        messageFormat: "ref struct cannot use JSON generation (ref structs cannot be generic type arguments)",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsafeOverlapRequiresUnsafe = new(
        id: "SPIRE_DU009",
        title: "UnsafeOverlap layout requires AllowUnsafeBlocks",
        messageFormat: "UnsafeOverlap layout requires <AllowUnsafeBlocks>true</AllowUnsafeBlocks> in the project",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor FieldNameTypeConflict = new(
        id: "SPIRE_DU010",
        title: "Field name conflict across variants",
        messageFormat: "{0}",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly Dictionary<string, DiagnosticDescriptor> DescriptorMap = new()
    {
        ["SPIRE_DU002"] = RefStructNotSupported,
        ["SPIRE_DU003"] = NoVariantsFound,
        ["SPIRE_DU004"] = LayoutIgnoredForRecord,
        ["SPIRE_DU005"] = OverlapOnGenericStruct,
        ["SPIRE_DU006"] = SystemTextJsonNotReferenced,
        ["SPIRE_DU007"] = NewtonsoftJsonNotReferenced,
        ["SPIRE_DU008"] = RefStructJsonNotSupported,
        ["SPIRE_DU009"] = UnsafeOverlapRequiresUnsafe,
        ["SPIRE_DU010"] = FieldNameTypeConflict,
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
