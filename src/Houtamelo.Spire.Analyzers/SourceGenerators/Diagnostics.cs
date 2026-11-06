using System.Collections.Generic;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

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
        title: "No [Variant] functions found",
        messageFormat: "No [Variant] functions found on discriminated union type",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
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

    private static readonly DiagnosticDescriptor FieldNameTypeConflict = new(
        id: "SPIRE_DU010",
        title: "Field name conflict across variants",
        messageFormat: "{0}",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ClassNotSupported = new(
        id: "SPIRE_DU011",
        title: "Class types are not supported for [DiscriminatedUnion]",
        messageFormat: "[DiscriminatedUnion] is not supported on class types. Consider using a record type (fully supported by the generator), C# 15 'union' types for a language-native alternative, or the [EnforceExhaustiveness] attribute for exhaustive switch checking on existing class hierarchies.",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor UnitySerializationDeclaration = UnityError(
        "SPIRE_DU013", "Union declaration cannot use Unity serialization");

    private static readonly DiagnosticDescriptor UnitySerializationMemberConflict = UnityError(
        "SPIRE_DU015", "Member conflicts with generated Unity serialization");

    private static readonly DiagnosticDescriptor UnitySerializationReference = UnityError(
        "SPIRE_DU016", "Unity serialization requires UnityEngine references");

    private static readonly DiagnosticDescriptor UnitySerializationVariantAlias = UnityError(
        "SPIRE_DU017", "Invalid Unity variant serialization alias");

    private static readonly DiagnosticDescriptor GenericContainingType = UnityError(
        "SPIRE_DU018", "Discriminated union cannot be nested in a generic type");

    private static DiagnosticDescriptor UnityError(string id, string title) => new(
        id: id,
        title: title,
        messageFormat: "{0}",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly Dictionary<string, DiagnosticDescriptor> DescriptorMap = new()
    {
        ["SPIRE_DU002"] = RefStructNotSupported,
        ["SPIRE_DU003"] = NoVariantsFound,
        ["SPIRE_DU006"] = SystemTextJsonNotReferenced,
        ["SPIRE_DU007"] = NewtonsoftJsonNotReferenced,
        ["SPIRE_DU008"] = RefStructJsonNotSupported,
        ["SPIRE_DU010"] = FieldNameTypeConflict,
        ["SPIRE_DU011"] = ClassNotSupported,
        ["SPIRE_DU013"] = UnitySerializationDeclaration,
        ["SPIRE_DU015"] = UnitySerializationMemberConflict,
        ["SPIRE_DU016"] = UnitySerializationReference,
        ["SPIRE_DU017"] = UnitySerializationVariantAlias,
        ["SPIRE_DU018"] = GenericContainingType,
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
