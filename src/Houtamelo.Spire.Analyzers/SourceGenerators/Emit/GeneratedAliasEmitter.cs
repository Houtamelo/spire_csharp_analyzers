using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Emits the short aliases shared by generated union sources and resolves the
/// payload-wrapper spelling when a source-level alias is not legal.
internal static class GeneratedAliasEmitter
{
    internal static void Emit(
        SourceBuilder sb,
        UnionDeclaration union,
        bool hasUnmanagedFields,
        bool hasFields)
    {
        AppendAlias(sb, union, "Serializable", "global::System.SerializableAttribute");
        AppendAlias(sb, union, "EditorBrowsable", "global::System.ComponentModel.EditorBrowsableAttribute");
        AppendAlias(sb, union, "EditorBrowsableState", "global::System.ComponentModel.EditorBrowsableState");
        AppendAlias(sb, union, "EnforceInitialization", "global::Houtamelo.Spire.EnforceInitializationAttribute");
        AppendAlias(sb, union, "EnforceExhaustiveness", "global::Houtamelo.Spire.EnforceExhaustivenessAttribute");

        if (hasUnmanagedFields)
        {
            AppendAlias(sb, union, "StructLayout", "global::System.Runtime.InteropServices.StructLayoutAttribute");
            AppendAlias(sb, union, "LayoutKind", "global::System.Runtime.InteropServices.LayoutKind");
        }

        if (union.EmitUnitySerialization)
        {
            AppendAlias(sb, union, "SpireSerialization", "global::Houtamelo.Spire.UnitySerializationBridge");
            AppendAlias(sb, union, "SerializeField", "global::UnityEngine.SerializeField");
            AppendAlias(sb, union, "SerializeReference", "global::UnityEngine.SerializeReference");
            AppendAlias(sb, union, "Preserve", "global::UnityEngine.Scripting.PreserveAttribute");
            AppendAlias(sb, union, "NonSerialized", "global::System.NonSerializedAttribute");
            AppendAlias(sb, union, "ISerializationCallbackReceiver", "global::UnityEngine.ISerializationCallbackReceiver");
            AppendAlias(sb, union, "UnitySerializationPayloadBase", "global::Houtamelo.Spire.UnitySerializationPayloadBase");
        }

        if (hasFields)
            AppendContainerAlias(sb, union, "Fields", "Fields");
        AppendContainerAlias(sb, union, "Variants", "Variants");
    }

    internal static string Reference(
        UnionDeclaration union,
        string alias,
        string fullyQualifiedTarget)
        => CanUseAlias(union, alias) ? alias : fullyQualifiedTarget;

    internal static string AttributeReference(
        UnionDeclaration union,
        string alias,
        string fullyQualifiedTarget)
        => CanUseAlias(union, alias) ? "@" + alias : fullyQualifiedTarget;

    internal static string SerializedPayloadReference(
        UnionDeclaration union,
        string variantName)
        => $"global::Houtamelo.Spire.Generated.SpireSerializedPayload<{VariantPayloadReference(union, variantName)}>";

    internal static string ConcreteSerializedPayloadReference(
        UnionDeclaration union,
        string variantName)
        => $"{GlobalTypesPath(union)}.SerializedPayloads.{IdentifierUtility.Escape(Safe(variantName))}SerializedPayload{DeclarationFormatting.FormatEscapedTypeParameters(union.TypeParameters)}";

    internal static string FieldsReference(UnionDeclaration union)
        => ContainerReference(union, "Fields", $"{GlobalTypesPath(union)}.Fields");

    internal static string VariantsReference(UnionDeclaration union)
        => ContainerReference(union, "Variants", $"{GlobalTypesPath(union)}.Variants");

    internal static string VariantPayloadReference(UnionDeclaration union, string variantName)
        => $"{VariantsReference(union)}.{IdentifierUtility.Escape(Safe(variantName))}{DeclarationFormatting.FormatEscapedTypeParameters(union.TypeParameters)}";

    private static void AppendAlias(
        SourceBuilder sb,
        UnionDeclaration union,
        string alias,
        string target)
    {
        if (CanUseAlias(union, alias))
            sb.AppendLine($"using {alias} = {target};");
    }

    private static void AppendContainerAlias(
        SourceBuilder sb,
        UnionDeclaration union,
        string alias,
        string containerName)
    {
        if (CanUseContainerAlias(union, alias))
            sb.AppendLine($"using {alias} = {GlobalTypesPath(union)}.{containerName};");
    }

    private static bool CanUseAlias(UnionDeclaration union, string alias)
        => !ReservedNames(union).Contains(IdentifierUtility.SerializedName(alias));

    private static string ContainerReference(
        UnionDeclaration union,
        string alias,
        string fullyQualifiedTarget)
        => CanUseContainerAlias(union, alias) ? alias : fullyQualifiedTarget;

    private static bool CanUseContainerAlias(UnionDeclaration union, string alias)
        => !union.ContainingTypes.Any(type => type.TypeParameters.Length > 0) &&
            IsPayloadAccessible(union) &&
            CanUseAlias(union, alias);

    private static bool IsPayloadAccessible(UnionDeclaration union)
    {
        if (union.ContainingTypes.Length > 0 && string.IsNullOrEmpty(union.AccessibilityKeyword))
            return false;
        if (IsInaccessible(union.AccessibilityKeyword))
            return false;

        for (var index = 0; index < union.ContainingTypes.Length; index++)
        {
            var containingType = union.ContainingTypes[index];
            if (index > 0 && string.IsNullOrEmpty(containingType.AccessibilityKeyword))
                return false;
            if (IsInaccessible(containingType.AccessibilityKeyword))
                return false;
        }

        return true;
    }

    private static bool IsInaccessible(string accessibility)
        => accessibility is "private" or "protected" or "private protected";

    private static HashSet<string> ReservedNames(UnionDeclaration union)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            IdentifierUtility.SerializedName(union.TypeName),
            IdentifierUtility.SerializedName(TypesName(union)),
        };

        foreach (var parameter in union.TypeParameters)
            names.Add(IdentifierUtility.SerializedName(parameter));

        foreach (var containingType in union.ContainingTypes)
        {
            names.Add(IdentifierUtility.SerializedName(containingType.Name));
            foreach (var parameter in containingType.TypeParameters)
                names.Add(IdentifierUtility.SerializedName(parameter));
        }

        foreach (var variant in union.Variants)
        {
            names.Add(IdentifierUtility.SerializedName(variant.Name));
            if (variant.HasFormerlySerializedAs)
            {
                names.Add(IdentifierUtility.SerializedName(variant.FormerlySerializedAs!));
            }
        }

        return names;
    }

    private static string GlobalTypesPath(UnionDeclaration union)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(union.Namespace))
        {
            parts.AddRange(union.Namespace
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(IdentifierUtility.Escape));
        }

        foreach (var containingType in union.ContainingTypes)
        {
            var typeName = IdentifierUtility.Escape(containingType.Name);
            parts.Add(typeName + DeclarationFormatting.FormatEscapedTypeParameters(containingType.TypeParameters));
        }

        parts.Add(IdentifierUtility.Escape(TypesName(union)));
        return "global::" + string.Join(".", parts);
    }

    internal static string TypesName(UnionDeclaration union)
        => union.TypeName + "Types" +
            (union.TypeParameters.Length == 0 ? string.Empty : union.TypeParameters.Length.ToString());

    private static string Safe(string name)
    {
        var raw = IdentifierUtility.SerializedName(name);
        var chars = raw
            .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();
        return chars.Length == 0 ? "Value" : new string(chars);
    }
}
