using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Emits the representation used by Unity's native serializer.
/// Runtime values remain in the normal layout storage; only the active typed
/// payload is serialized.
internal static class UnitySerializationEmitter
{
    private const string ReconstructedPayloadType = "global::Houtamelo.Spire.Generated.__SpireReconstructedPayload";
    private const string ReconstructedPayloadInstance = ReconstructedPayloadType + ".Instance";
    internal const string RuntimeKindFieldName = "_kind";

    /// Emits the single active managed-reference cache used by the variant-payload
    /// representation. The shared wrapper declaration is emitted once per
    /// compilation by UnitySerializationSupportEmitter.
    internal static void EmitVariantSerializedStorage(SourceBuilder sb, UnionDeclaration union)
    {
        var serializeReference = GeneratedAliasEmitter.AttributeReference(
            union,
            "SerializeReference",
            "global::UnityEngine.SerializeReference");
        var payloadBase = GeneratedAliasEmitter.Reference(
            union,
            "UnitySerializationPayloadBase",
            "global::Houtamelo.Spire.UnitySerializationPayloadBase");
        sb.AppendLine($"[{serializeReference}] {payloadBase}? _serializedPayload;");
        sb.AppendLine();
    }

    /// Emits callbacks for a union whose public payload structs are themselves
    /// Unity-serializable. This path intentionally does not emit per-variant
    /// serializer classes: each cache value is a closed shared wrapper.
    internal static void EmitVariantPayloadCallbacks(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType)
    {
        EmitVariantSerializedStateHelpers(sb, union);
        EmitVariantCallbacks(sb, union, unionType);
    }

    private static void EmitVariantSerializedStateHelpers(SourceBuilder sb, UnionDeclaration union)
    {
        AppendHidden(sb, union, "public readonly bool __SpireSerializedStateCurrent()");
        sb.OpenBrace();
        sb.AppendLine("return this.__SpireSerializedStateCurrent(this._serializedPayload);");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, "public readonly bool __SpireSerializedStateCurrent(object? payload) => (this._kind, payload) switch");
        sb.OpenBrace();
        var factoryNames = union.Variants.Select(candidate => candidate.Name).ToArray();
        foreach (var variant in union.Variants)
        {
            var payloadVariable = "serializedPayload_" + Safe(variant.Name);
            var wrapperType = GeneratedAliasEmitter.SerializedPayloadReference(union, variant.Name);
            var arm = $"(Kind.{IdentifierUtility.Escape(variant.Name)}, {wrapperType} {payloadVariable}) =>";
            if (variant.Fields.Length == 0)
            {
                sb.AppendLine(arm + " true,");
                continue;
            }
            sb.AppendLine(arm);

            var accessor = VariantNaming.AccessorName(variant.Name, factoryNames);
            var bridge = GeneratedAliasEmitter.Reference(
                union,
                "SpireSerialization",
                "global::Houtamelo.Spire.UnitySerializationBridge");
            var matches = variant.Fields.Select(field =>
            {
                var runtime = $"this.{IdentifierUtility.Escape(accessor)}.{IdentifierUtility.Escape(field.Name)}";
                var serialized = $"{payloadVariable}.value.{IdentifierUtility.Escape(field.Name)}";
                var comparer = field.IsUnityManagedReference
                    ? "SpireSerializedManagedReferenceValuesEqual"
                    : "SpireSerializedValuesEqual";
                return $"{bridge}.{comparer}((object?){runtime}, (object?){serialized})";
            }).ToArray();
            sb.Indent();
            for (var index = 0; index < matches.Length; index++)
                sb.AppendLine($"{(index == 0 ? string.Empty : "&& ")}{matches[index]}{(index + 1 == matches.Length ? "," : string.Empty)}");
            sb.Dedent();
        }
        sb.AppendLine("_ => false,");
        sb.CloseBrace(";");
        sb.AppendLine();
    }

    private static void EmitVariantCallbacks(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType)
    {
        AppendHidden(sb, union, $"void {GeneratedAliasEmitter.Reference(union, "ISerializationCallbackReceiver", "global::UnityEngine.ISerializationCallbackReceiver")}.OnBeforeSerialize() => this = this.__SpirePrepareForSerialization();");
        sb.AppendLine();

        AppendHidden(sb, union, $"public {unionType} __SpirePrepareForSerialization()");
        sb.OpenBrace();
        sb.AppendLine($"switch (this.{RuntimeKindFieldName})");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            var variantName = IdentifierUtility.Escape(variant.Name);
            var payloadType = VariantPayloadType(union, variant.Name);
            var wrapperType = GeneratedAliasEmitter.SerializedPayloadReference(union, variant.Name);
            var currentVariable = "currentPayload_" + Safe(variant.Name);
            var aliasVariable = "formerPayload_" + Safe(variant.FormerlySerializedAs ?? variant.Name);
            var preparedVariable = "preparedPayload_" + Safe(variant.Name);
            var preparedValueVariable = "preparedValue_" + Safe(variant.Name);

            sb.AppendLine($"case Kind.{variantName}:");
            sb.Indent();
            sb.AppendLine($"var {currentVariable} = this._serializedPayload as {wrapperType};");
            if (HasAlias(variant))
            {
                var aliasType = GeneratedAliasEmitter.SerializedPayloadReference(union, variant.FormerlySerializedAs!);
                sb.AppendLine($"var {aliasVariable} = this._serializedPayload as {aliasType};");
            }
            sb.AppendLine($"if ({currentVariable} is not null && this.__SpireSerializedStateCurrent({currentVariable}))");
            sb.Indent();
            sb.AppendLine("return this;");
            sb.Dedent();
            sb.AppendLine($"var {preparedVariable} = new {wrapperType}();");
            EmitVariantPayloadRefresh(
                sb,
                union,
                variant,
                payloadType,
                preparedValueVariable,
                currentVariable,
                HasAlias(variant) ? aliasVariable : null);
            sb.AppendLine($"{preparedVariable}.value = {preparedValueVariable};");
            sb.AppendLine($"this._serializedPayload = {preparedVariable};");
            sb.AppendLine("return this;");
            sb.Dedent();
        }
        sb.AppendLine("default:");
        sb.Indent();
        sb.AppendLine("this._serializedPayload = null;");
        sb.AppendLine("return this;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, $"void {GeneratedAliasEmitter.Reference(union, "ISerializationCallbackReceiver", "global::UnityEngine.ISerializationCallbackReceiver")}.OnAfterDeserialize()");
        sb.OpenBrace();
        sb.AppendLine("var rawPayload = this._serializedPayload;");
        sb.AppendLine($"var validPayload = __SpireTryFromSerialized(rawPayload, out {unionType} restored);");
        sb.AppendLine("this = restored;");
        sb.AppendLine("if (!validPayload)");
        sb.OpenBrace();
        var fallbackName = union.Variants.Length == 0
            ? "<Invalid>"
            : IdentifierUtility.SerializedName(union.Variants[0].Name);
        sb.AppendLine($"global::UnityEngine.Debug.LogWarning(\"invalid union serialization data, defaulting to {fallbackName}\");");
        sb.CloseBrace();
        sb.AppendLine("#if !UNITY_EDITOR");
        sb.AppendLine($"this._serializedPayload = {ReconstructedPayloadInstance};");
        sb.AppendLine("#endif");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, $"static bool __SpireTryFromSerialized(object? payload, out {unionType} restored)");
        sb.OpenBrace();
        sb.AppendLine("switch (payload)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            EmitVariantPayloadRestoreBranch(sb, union, variant, variant.Name, "payload");
            if (HasAlias(variant))
                EmitVariantPayloadRestoreBranch(sb, union, variant, variant.FormerlySerializedAs!, "payload");
        }

        sb.AppendLine("default:");
        sb.Indent();
        if (union.Variants.Length == 0)
        {
            sb.AppendLine($"restored = default({unionType});");
        }
        else
        {
            var first = union.Variants[0];
            var fallbackVariable = "fallback_" + Safe(first.Name);
            var defaults = string.Join(", ", first.Fields.Select(field => $"default({field.TypeFullName})!"));
            sb.AppendLine($"var {fallbackVariable} = {IdentifierUtility.Escape(first.Name)}({defaults});");
            sb.AppendLine($"{fallbackVariable}._serializedPayload = null;");
            sb.AppendLine($"restored = {fallbackVariable};");
        }
        sb.AppendLine("return false;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, $"public static {unionType} __SpireFromSerialized(object? payload)");
        sb.OpenBrace();
        sb.AppendLine($"__SpireTryFromSerialized(payload, out {unionType} restored);");
        sb.AppendLine("return restored;");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, $"public readonly {unionType} __SpireHydrateSerialized()");
        sb.OpenBrace();
        sb.AppendLine($"if (this._serializedPayload is {ReconstructedPayloadType})");
        sb.Indent();
        sb.AppendLine("return this;");
        sb.Dedent();
        sb.AppendLine("var hydrated = __SpireFromSerialized(this._serializedPayload);");
        sb.AppendLine("#if !UNITY_EDITOR");
        sb.AppendLine($"hydrated._serializedPayload = {ReconstructedPayloadInstance};");
        sb.AppendLine("#endif");
        sb.AppendLine("return hydrated;");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, union, $"public static {unionType} __SpireCreateDefault(int kind)");
        sb.OpenBrace();
        sb.AppendLine("switch ((Kind)kind)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            sb.AppendLine($"case Kind.{IdentifierUtility.Escape(variant.Name)}:");
            sb.Indent();
            sb.AppendLine($"return {IdentifierUtility.Escape(variant.Name)}({string.Join(", ", variant.Fields.Select(field => $"default({field.TypeFullName})!"))});");
            sb.Dedent();
        }
        sb.AppendLine("default:");
        sb.Indent();
        sb.AppendLine($"var unknown = default({unionType});");
        sb.AppendLine($"unknown.{RuntimeKindFieldName} = (Kind)kind;");
        sb.AppendLine("return unknown;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static void EmitVariantPayloadRefresh(
        SourceBuilder sb,
        UnionDeclaration union,
        VariantInfo variant,
        string payloadType,
        string preparedValueVariable,
        string currentVariable,
        string? aliasVariable)
    {
        var factoryNames = union.Variants.Select(candidate => candidate.Name).ToArray();
        var accessor = VariantNaming.AccessorName(variant.Name, factoryNames);
        var values = new List<string>();
        foreach (var field in variant.Fields)
        {
            var fieldName = IdentifierUtility.Escape(field.Name);
            var property = $"this.{IdentifierUtility.Escape(accessor)}.{fieldName}";
            var prior = PriorPayloadFieldExpression(currentVariable, aliasVariable, fieldName);
            string value;
            if (field.IsUnityManagedReference)
            {
                value = $"({field.TypeFullName}){Bridge(union)}.PrepareManagedReference((object?){property}, {prior})!";
            }
            else if (field.RequiresUnityRuntimeDispatch)
            {
                value = $"({field.TypeFullName}){Bridge(union)}.PrepareDynamic((object?){property}, {prior})!";
            }
            else if (field.IsUnityUnion || field.IsUnityUnionCollection || field.IsUnityComposite)
            {
                value = $"({field.TypeFullName}){Bridge(union)}.Prepare((object?){property}, {prior})!";
            }
            else
            {
                value = property;
            }
            values.Add(value);
        }
        sb.AppendLine($"var {preparedValueVariable} = new {payloadType}({string.Join(", ", values)});");
    }

    private static string PriorPayloadFieldExpression(
        string currentVariable,
        string? aliasVariable,
        string fieldName)
    {
        if (aliasVariable is null)
            return $"{currentVariable} is null ? (object?)null : (object?){currentVariable}.value.{fieldName}";

        return $"{currentVariable} is not null ? (object?){currentVariable}.value.{fieldName} : " +
            $"{aliasVariable} is null ? (object?)null : (object?){aliasVariable}.value.{fieldName}";
    }

    private static void EmitVariantPayloadRestoreBranch(
        SourceBuilder sb,
        UnionDeclaration union,
        VariantInfo variant,
        string payloadVariantName,
        string payloadExpression)
    {
        var payloadVariable = "typedPayload_" + Safe(payloadVariantName);
        var restoredVariable = "restoredVariant_" + Safe(payloadVariantName);
        sb.AppendLine($"case {GeneratedAliasEmitter.SerializedPayloadReference(union, payloadVariantName)} {payloadVariable}:");
        sb.Indent();
        var args = variant.Fields.Select(field => RestoreVariantFieldExpression(
            union,
            field,
            $"{payloadVariable}.value.{IdentifierUtility.Escape(field.Name)}"));
        sb.AppendLine($"var {restoredVariable} = {IdentifierUtility.Escape(variant.Name)}({string.Join(", ", args)});");
        sb.AppendLine($"{restoredVariable}._serializedPayload = {payloadVariable};");
        sb.AppendLine($"restored = {restoredVariable};");
        sb.AppendLine("return true;");
        sb.Dedent();
    }

    private static string RestoreVariantFieldExpression(
        UnionDeclaration union,
        FieldInfo field,
        string expression)
    {
        if (field.IsUnityManagedReference)
            return $"({field.TypeFullName}){Bridge(union)}.RestoreManagedReference((object?){expression})!";

        if (field.RequiresUnityRuntimeDispatch)
            return $"({field.TypeFullName}){Bridge(union)}.RestoreDynamic((object?){expression})!";

        if (field.IsUnityUnion)
            return expression + ".__SpireHydrateSerialized()";

        if (field.IsUnityUnionCollection || field.IsUnityComposite)
            return $"({field.TypeFullName}){Bridge(union)}.Restore((object?){expression})!";

        return expression;
    }

    private static string VariantPayloadType(UnionDeclaration union, string variantName)
        => GeneratedAliasEmitter.VariantPayloadReference(union, variantName);

    private static bool HasAlias(VariantInfo variant)
        => variant.HasFormerlySerializedAs;

    private static string Bridge(UnionDeclaration union)
        => GeneratedAliasEmitter.Reference(
            union,
            "SpireSerialization",
            "global::Houtamelo.Spire.UnitySerializationBridge");

    private static void AppendHidden(
        SourceBuilder sb,
        UnionDeclaration union,
        string declaration)
    {
        var editorBrowsable = GeneratedAliasEmitter.AttributeReference(
            union,
            "EditorBrowsable",
            "global::System.ComponentModel.EditorBrowsableAttribute");
        var editorBrowsableState = GeneratedAliasEmitter.Reference(
            union,
            "EditorBrowsableState",
            "global::System.ComponentModel.EditorBrowsableState");
        sb.AppendLine($"[{editorBrowsable}({editorBrowsableState}.Never)]");
        sb.AppendLine(declaration);
    }

    private static string Safe(string name)
    {
        var raw = IdentifierUtility.SerializedName(name);
        var chars = raw.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        return chars.Length == 0 ? "Value" : new string(chars);
    }
}
