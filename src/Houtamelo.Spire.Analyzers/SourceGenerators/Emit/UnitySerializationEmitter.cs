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
    private const string PayloadBaseName = "__SpireSerializedPayloadBase";
    private const string ReconstructedPayloadName = "__SpireReconstructedPayload";
    internal const string RuntimeKindFieldName = "_kind";

    public static string Emit(UnionDeclaration union, bool hasInlineArray = false)
        => union.Strategy switch
        {
            EmitStrategy.BoxedFields => BoxedFieldsEmitter.EmitUnity(union),
            EmitStrategy.BoxedTuple => BoxedTupleEmitter.EmitUnity(union),
            EmitStrategy.UnsafeOverlap => UnsafeOverlapEmitter.EmitUnity(union, hasInlineArray),
            EmitStrategy.Overlap => OverlapEmitter.EmitUnity(union),
            _ => AdditiveEmitter.EmitCore(union, unitySerialization: true),
        };

    public static void EmitSerializedStorage(SourceBuilder sb, UnionDeclaration union)
    {
        _ = union;
        sb.AppendLine("[global::UnityEngine.SerializeReference]");
        sb.AppendLine($"private {PayloadBaseName}? _serializedPayload;");
        sb.AppendLine();
    }

    public static void EmitCallbacksAndPayloads(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType,
        IEnumerable<string> runtimeResetStatements)
    {
        sb.AppendLine("[global::UnityEngine.Scripting.Preserve]");
        sb.AppendLine($"private sealed class {ReconstructedPayloadName} : {PayloadBaseName} {{ }}");
        sb.AppendLine($"private static readonly {PayloadBaseName} {ReconstructedPayloadName}Instance = new {ReconstructedPayloadName}();");
        sb.AppendLine();
        EmitSerializedStateHelpers(sb, union);
        EmitCallbacks(sb, union, unionType);
        EmitRuntimeResetMethod(sb, runtimeResetStatements);
        EmitPayloadTypes(sb, union);
    }

    private static void EmitSerializedStateHelpers(SourceBuilder sb, UnionDeclaration union)
    {
        AppendHidden(sb, "public bool __SpireSerializedStateCurrent()");
        sb.OpenBrace();
        sb.AppendLine("return this.__SpireSerializedStateCurrent(this._serializedPayload);");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, "public bool __SpireSerializedStateCurrent(object? payload)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            var payloadVariable = "payload_" + Safe(variant.Name);
            EmitStateBranch(sb, union, variant, PayloadName(variant.Name), payloadVariable);
            if (HasAlias(variant))
            {
                var aliasVariable = "payload_" + Safe(variant.FormerlySerializedAs!);
                EmitStateBranch(sb, union, variant, PayloadName(variant.FormerlySerializedAs!), aliasVariable);
            }
        }
        sb.AppendLine("return false;");
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static void EmitStateBranch(
        SourceBuilder sb,
        UnionDeclaration union,
        VariantInfo variant,
        string payloadType,
        string payloadVariable)
    {
        sb.AppendLine($"if (payload is {payloadType} {payloadVariable})");
        sb.OpenBrace();
        sb.AppendLine($"if (this.kind != Kind.{IdentifierUtility.Escape(variant.Name)}) return false;");
        if (variant.Fields.Length == 0)
            sb.AppendLine("return true;");
        else
        {
            var matches = variant.Fields.Select(field =>
                SerializedFieldMatchExpression(union, field, payloadVariable));
            sb.AppendLine("return " + string.Join(" && ", matches) + ";");
        }
        sb.CloseBrace();
    }

    private static string SerializedFieldMatchExpression(
        UnionDeclaration union,
        FieldInfo field,
        string payloadVariable)
    {
        var runtimeProperty = UsesSafeBoxedFieldAccess(union)
            ? $"this.__SpireGetSerializedField_{Safe(field.ResolvedPropertyName)}()"
            : $"this.{IdentifierUtility.Escape(field.ResolvedPropertyName)}";
        var payloadField = $"{payloadVariable}.{IdentifierUtility.Escape(field.Name)}";
        var comparer = field.IsUnityManagedReference
            ? "__SpireSerializedManagedReferenceValuesEqual"
            : "__SpireSerializedValuesEqual";
        return $"global::Houtamelo.Spire.UnitySerializationBridge.{comparer}((object?){runtimeProperty}, (object?){payloadField})";
    }

    private static void EmitCallbacks(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType)
    {
        AppendHidden(sb, "public void OnBeforeSerialize() => this = this.__SpirePrepareForSerialization();");
        sb.AppendLine();

        AppendHidden(sb, $"public {unionType} __SpirePrepareForSerialization()");
        sb.OpenBrace();
        sb.AppendLine($"switch (this.{RuntimeKindFieldName})");
        sb.OpenBrace();

        foreach (var variant in union.Variants)
        {
            var variantName = IdentifierUtility.Escape(variant.Name);
            var payloadName = PayloadName(variant.Name);
            var currentPayload = "currentPayload_" + Safe(variant.Name);
            var preparedPayload = "preparedPayload_" + Safe(variant.Name);
            sb.AppendLine($"case Kind.{variantName}:");
            sb.Indent();
            sb.AppendLine($"var {currentPayload} = this._serializedPayload as {payloadName};");
            sb.AppendLine($"if ({currentPayload} is not null && this.__SpireSerializedStateCurrent({currentPayload}))");
            sb.Indent();
            sb.AppendLine("return this;");
            sb.Dedent();
            sb.AppendLine($"var {preparedPayload} = new {payloadName}();");
            EmitPayloadRefresh(sb, union, variant, preparedPayload, currentPayload);
            sb.AppendLine($"this._serializedPayload = {preparedPayload};");
            sb.AppendLine("return this;");
            sb.Dedent();
        }

        sb.AppendLine("default:");
        sb.Indent();
        sb.AppendLine("this._serializedPayload = null;");
        sb.AppendLine("this.__SpireResetRuntimeFields();");
        sb.AppendLine("return this;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, "public void OnAfterDeserialize()");
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
        sb.AppendLine($"this._serializedPayload = {ReconstructedPayloadName}Instance;");
        sb.AppendLine("#endif");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, $"private static bool __SpireTryFromSerialized(object? payload, out {unionType} restored)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            EmitPayloadRestoreBranch(sb, variant, PayloadName(variant.Name), "payload");
            if (HasAlias(variant))
                EmitPayloadRestoreBranch(sb, variant, PayloadName(variant.FormerlySerializedAs!), "payload");
        }

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
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, $"public static {unionType} __SpireFromSerialized(object? payload)");
        sb.OpenBrace();
        sb.AppendLine($"__SpireTryFromSerialized(payload, out {unionType} restored);");
        sb.AppendLine("return restored;");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, $"public {unionType} __SpireHydrateSerialized()");
        sb.OpenBrace();
        sb.AppendLine($"if (this._serializedPayload is {ReconstructedPayloadName})");
        sb.Indent();
        sb.AppendLine("return this;");
        sb.Dedent();
        sb.AppendLine("var hydrated = __SpireFromSerialized(this._serializedPayload);");
        sb.AppendLine("#if !UNITY_EDITOR");
        sb.AppendLine($"hydrated._serializedPayload = {ReconstructedPayloadName}Instance;");
        sb.AppendLine("#endif");
        sb.AppendLine("return hydrated;");
        sb.CloseBrace();
        sb.AppendLine();

        AppendHidden(sb, $"public static {unionType} __SpireCreateDefault(int kind)");
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
        sb.AppendLine("var unknown = default(" + unionType + ");");
        sb.AppendLine($"unknown.{RuntimeKindFieldName} = (Kind)kind;");
        sb.AppendLine("return unknown;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static void EmitPayloadRestoreBranch(
        SourceBuilder sb,
        VariantInfo variant,
        string payloadType,
        string payloadExpression)
    {
        var payloadVariable = "typedPayload_" + Safe(payloadType);
        sb.AppendLine($"if ({payloadExpression} is {payloadType} {payloadVariable})");
        sb.OpenBrace();
        var args = variant.Fields
            .Select(field => RestoreExpression(
                field,
                $"{payloadVariable}.{IdentifierUtility.Escape(field.Name)}",
                variant.Name))
            .ToArray();
        sb.AppendLine($"var restoredVariant = {IdentifierUtility.Escape(variant.Name)}({string.Join(", ", args)});");
        sb.AppendLine($"restoredVariant._serializedPayload = {payloadExpression} as {PayloadBaseName};");
        sb.AppendLine("restored = restoredVariant;");
        sb.AppendLine("return true;");
        sb.CloseBrace();
    }

    private static bool HasAlias(VariantInfo variant)
        => !string.IsNullOrEmpty(variant.FormerlySerializedAs) &&
            !string.Equals(
                variant.FormerlySerializedAs,
                variant.Name,
                StringComparison.Ordinal);

    private static void EmitPayloadRefresh(
        SourceBuilder sb,
        UnionDeclaration union,
        VariantInfo variant,
        string payloadVariable,
        string priorPayloadVariable)
    {
        foreach (var field in variant.Fields)
        {
            var property = UsesSafeBoxedFieldAccess(union)
                ? $"this.__SpireGetSerializedField_{Safe(field.ResolvedPropertyName)}()"
                : "this." + IdentifierUtility.Escape(field.ResolvedPropertyName);
            var payload = payloadVariable + "." + IdentifierUtility.Escape(field.Name);
            var prior = $"({priorPayloadVariable} is null ? (object?)null : (object?){priorPayloadVariable}.{IdentifierUtility.Escape(field.Name)})";

            if (field.IsUnityManagedReference)
            {
                sb.AppendLine($"{payload} = ({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.PrepareManagedReference((object?){property}, {prior})!;");
            }
            else if (field.RequiresUnityRuntimeDispatch)
            {
                sb.AppendLine($"{payload} = ({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.PrepareDynamic((object?){property}, {prior})!;");
            }
            else if (field.IsUnityUnion || field.IsUnityUnionCollection || field.IsUnityComposite)
            {
                sb.AppendLine($"{payload} = ({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.Prepare((object?){property}, {prior})!;");
            }
            else
            {
                sb.AppendLine($"{payload} = {property};");
            }
        }
    }

    private static bool UsesSafeBoxedFieldAccess(UnionDeclaration union)
        => union.Strategy is EmitStrategy.BoxedFields or EmitStrategy.BoxedTuple;

    private static void EmitRuntimeResetMethod(
        SourceBuilder sb,
        IEnumerable<string> runtimeResetStatements)
    {
        sb.AppendLine("private void __SpireResetRuntimeFields()");
        sb.OpenBrace();
        foreach (var statement in runtimeResetStatements)
            sb.AppendLine(statement);
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static string RestoreExpression(FieldInfo field, string expression, string variantName)
    {
        if (field.IsUnityManagedReference)
            return $"({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.RestoreManagedReference((object?){expression})!";

        if (field.RequiresUnityRuntimeDispatch)
            return $"({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.RestoreDynamic((object?){expression})!";

        if (field.IsUnityUnion)
            return expression + ".__SpireHydrateSerialized()";

        if (field.IsUnityUnionCollection || field.IsUnityComposite)
            return $"({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.Restore((object?){expression})!";

        _ = variantName;
        return expression;
    }

    private static void EmitPayloadTypes(SourceBuilder sb, UnionDeclaration union)
    {
        sb.AppendLine("[global::System.Serializable]");
        sb.AppendLine($"private abstract class {PayloadBaseName} : global::Houtamelo.Spire.UnitySerializationPayloadBase");
        sb.OpenBrace();
        sb.CloseBrace();
        sb.AppendLine();

        foreach (var variant in union.Variants)
        {
            EmitPayloadType(
                sb,
                variant,
                PayloadName(variant.Name),
                isFormerNamePayload: false);
            if (HasAlias(variant))
                EmitPayloadType(
                    sb,
                    variant,
                    PayloadName(variant.FormerlySerializedAs!),
                    isFormerNamePayload: true);
        }
    }

    private static void EmitPayloadType(
        SourceBuilder sb,
        VariantInfo variant,
        string payloadName,
        bool isFormerNamePayload)
    {
        if (isFormerNamePayload)
            sb.AppendLine("[global::UnityEngine.Scripting.Preserve]");
        sb.AppendLine("[global::System.Serializable]");
        sb.AppendLine($"private sealed class {payloadName} : {PayloadBaseName}");
        sb.OpenBrace();
        if (isFormerNamePayload)
        {
            sb.AppendLine("[global::UnityEngine.Scripting.Preserve]");
            sb.AppendLine($"public {payloadName}() {{ }}");
        }
        foreach (var field in variant.Fields)
        {
            sb.AppendLine("[global::UnityEngine.SerializeField]");
            if (field.IsUnityManagedReference)
                sb.AppendLine("[global::UnityEngine.SerializeReference]");
            sb.AppendLine($"public {field.TypeFullName} {IdentifierUtility.Escape(field.Name)} = default!;");
        }
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static void AppendHidden(SourceBuilder sb, string declaration)
    {
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine(declaration);
    }

    private static string PayloadName(string variantName)
        => "__Spire" + Safe(variantName) + "Payload";

    private static string Safe(string name)
    {
        var raw = IdentifierUtility.SerializedName(name);
        var chars = raw.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        return chars.Length == 0 ? "Value" : new string(chars);
    }
}
