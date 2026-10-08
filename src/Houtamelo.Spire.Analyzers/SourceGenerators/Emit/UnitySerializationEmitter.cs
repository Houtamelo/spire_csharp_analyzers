using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Emits the Additive representation used by Unity's native serializer.
/// Runtime values remain in the normal Additive slots; only the discriminator and
/// active typed payload are serialized.
internal static class UnitySerializationEmitter
{
    private const string PayloadBaseName = "__SpireSerializedPayload";

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
        sb.AppendLine("private sealed class __SpireSerializationOwnerToken { }");
        sb.AppendLine("[global::UnityEngine.SerializeReference]");
        sb.AppendLine($"private {PayloadBaseName}? _serializedPayload;");
        sb.AppendLine("[global::System.NonSerialized]");
        sb.AppendLine("private __SpireSerializationOwnerToken? __SpireSerializationOwner;");
        sb.AppendLine();
    }

    public static void EmitCallbacksAndPayloads(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType,
        IEnumerable<string> runtimeResetStatements)
    {
        EmitDetachMethod(sb, union);
        EmitCallbacks(sb, union, unionType, runtimeResetStatements);
        EmitHydrationHelpers(sb, union);
        EmitPayloadTypes(sb, union);
    }

    private static void EmitDetachMethod(SourceBuilder sb, UnionDeclaration union)
    {
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine("private void __SpireDetachSerializedCache(int slot)");
        sb.OpenBrace();
        sb.AppendLine("_ = slot;");
        sb.AppendLine("this._serializedPayload = null;");
        sb.AppendLine("this.__SpireSerializationOwner = null;");
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine("private object __SpireEnsureSerializationOwner()");
        sb.OpenBrace();
        sb.AppendLine("if (this.__SpireSerializationOwner is null)");
        sb.Indent();
        sb.AppendLine("this.__SpireSerializationOwner = new __SpireSerializationOwnerToken();");
        sb.Dedent();
        sb.AppendLine("return this.__SpireSerializationOwner;");
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine("public bool __SpireSerializedStateCurrent()");
        sb.OpenBrace();
        sb.AppendLine("switch (this._kind)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            sb.AppendLine($"case Kind.{IdentifierUtility.Escape(variant.Name)}:");
            sb.Indent();
            sb.AppendLine($"if (this._serializedPayload is not {PayloadName(variant.Name)} payload_{Safe(variant.Name)}) return false;");
            if (variant.Fields.Length == 0)
                sb.AppendLine("return true;");
            else
                sb.AppendLine("return " + string.Join(" && ", variant.Fields.Select(field => SlotMatchesExpression(field, Safe(variant.Name)))) + ";");
            sb.Dedent();
        }
        sb.AppendLine("default: return true;");
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static string SlotMatchesExpression(FieldInfo field, string variant)
    {
        if (field.IsUnityUnion)
            return $"this.{IdentifierUtility.Escape(field.Name)}.__SpireSerializedStateCurrent()";

        if (field.IsUnityUnionCollection)
            return "false";

        return $"global::System.Collections.Generic.EqualityComparer<{field.TypeFullName}>.Default.Equals(this.{IdentifierUtility.Escape(field.Name)}, payload_{variant}.{IdentifierUtility.Escape(field.Name)})";
    }

    private static void EmitCallbacks(
        SourceBuilder sb,
        UnionDeclaration union,
        string unionType,
        IEnumerable<string> runtimeResetStatements)
    {
        sb.AppendLine("public void OnBeforeSerialize() => this.__SpirePrepareForSerialization();");
        sb.AppendLine();
        sb.AppendLine("public void __SpirePrepareForSerialization()");
        sb.OpenBrace();
        sb.AppendLine("var serializationOwner = this.__SpireEnsureSerializationOwner();");
        sb.AppendLine("switch (this._kind)");
        sb.OpenBrace();

        foreach (var variant in union.Variants)
        {
            var variantName = IdentifierUtility.Escape(variant.Name);
            var payloadName = PayloadName(variant.Name);
            var payloadVariable = "payload_" + Safe(variant.Name);
            sb.AppendLine($"case Kind.{variantName}:");
            sb.Indent();
            sb.AppendLine($"if (this._serializedPayload is not {payloadName} {payloadVariable} || !{payloadVariable}.__SpireTryClaim(serializationOwner))");
            sb.OpenBrace();
            sb.AppendLine($"{payloadVariable} = new {payloadName}();");
            sb.AppendLine($"this._serializedPayload = {payloadVariable};");
            sb.AppendLine($"{payloadVariable}.__SpireTryClaim(serializationOwner);");
            sb.CloseBrace();
            EmitPayloadRefresh(sb, variant, payloadVariable);
            sb.AppendLine("break;");
            sb.Dedent();
        }

        sb.AppendLine("default:");
        sb.Indent();
        sb.AppendLine("this._serializedPayload = null;");
        sb.AppendLine("this.__SpireResetRuntimeFields();");
        sb.AppendLine("break;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine("public void OnAfterDeserialize()");
        sb.OpenBrace();
        sb.AppendLine("var rawKind = (int)this._kind;");
        sb.AppendLine("this = __SpireFromSerialized(rawKind, this._serializedPayload);");
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine("private void __SpireResetRuntimeFields()");
        sb.OpenBrace();
        foreach (var statement in runtimeResetStatements)
            sb.AppendLine(statement);
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine($"public static {unionType} __SpireFromSerialized(int kind, object? payload)");
        sb.OpenBrace();
        sb.AppendLine("var rawPayload = payload;");
        sb.AppendLine("switch ((Kind)kind)");
        sb.OpenBrace();
        foreach (var variant in union.Variants)
        {
            var payloadName = PayloadName(variant.Name);
            var payloadVariable = "typedPayload_" + Safe(variant.Name);
            var missingVariable = "missing_" + Safe(variant.Name);
            sb.AppendLine($"case Kind.{IdentifierUtility.Escape(variant.Name)}:");
            sb.Indent();
            sb.AppendLine($"if (rawPayload is {payloadName} {payloadVariable})");
            sb.OpenBrace();
            var args = variant.Fields
                .Select(field => RestoreExpression(field, $"{payloadVariable}.{IdentifierUtility.Escape(field.Name)}", variant.Name))
                .ToArray();
            sb.AppendLine($"var restored = {IdentifierUtility.Escape(variant.Name)}({string.Join(", ", args)});");
            sb.AppendLine("restored._serializedPayload = rawPayload as __SpireSerializedPayload;");
            sb.AppendLine("restored._serializedPayload?.__SpireResetOwner();");
            sb.AppendLine("return restored;");
            sb.CloseBrace();
            var defaults = string.Join(", ", variant.Fields.Select(field => $"default({field.TypeFullName})!"));
            sb.AppendLine($"var {missingVariable} = {IdentifierUtility.Escape(variant.Name)}({defaults});");
            sb.AppendLine($"{missingVariable}._serializedPayload = rawPayload as __SpireSerializedPayload;");
            sb.AppendLine($"{missingVariable}._serializedPayload?.__SpireResetOwner();");
            sb.AppendLine($"return {missingVariable};");
            sb.Dedent();
        }

        sb.AppendLine("default:");
        sb.Indent();
        sb.AppendLine("var unknown = default(" + unionType + ");");
        sb.AppendLine("unknown._kind = (Kind)kind;");
        sb.AppendLine("unknown._serializedPayload = rawPayload as __SpireSerializedPayload;");
        sb.AppendLine("unknown.__SpireSerializationOwner = null;");
        sb.AppendLine("unknown._serializedPayload?.__SpireResetOwner();");
        sb.AppendLine("unknown.__SpireResetRuntimeFields();");
        sb.AppendLine("return unknown;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine($"public {unionType} __SpireHydrateSerialized()");
        sb.OpenBrace();
        sb.AppendLine("var rawKind = (int)this._kind;");
        sb.AppendLine("return __SpireFromSerialized(rawKind, this._serializedPayload);");
        sb.CloseBrace();
        sb.AppendLine();

        sb.AppendLine($"public static {unionType} __SpireCreateDefault(int kind)");
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
        sb.AppendLine("unknown._kind = (Kind)kind;");
        sb.AppendLine("return unknown;");
        sb.Dedent();
        sb.CloseBrace();
        sb.CloseBrace();
        sb.AppendLine();
    }

    private static void EmitPayloadRefresh(SourceBuilder sb, VariantInfo variant, string payloadVariable)
    {
        foreach (var field in variant.Fields)
        {
            var property = "this." + IdentifierUtility.Escape(field.Name);
            var payload = payloadVariable + "." + IdentifierUtility.Escape(field.Name);

            if (field.RequiresUnityRuntimeDispatch)
            {
                sb.AppendLine($"{payload} = ({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.PrepareDynamic({property}, {payload})!;");
            }
            else if (field.IsUnityUnion)
            {
                sb.AppendLine($"{payload} = global::Houtamelo.Spire.UnitySerializationBridge.Prepare({property}, {payload});");
            }
            else if (field.IsUnityUnionCollection)
            {
                EmitCollectionRefresh(sb, field, property, payload);
            }
            else if (field.IsUnityComposite)
            {
                sb.AppendLine($"{payload} = global::Houtamelo.Spire.UnitySerializationBridge.Prepare({property}, {payload});");
            }
            else
            {
                sb.AppendLine($"{payload} = {property};");
            }
        }
    }

    private static void EmitCollectionRefresh(
        SourceBuilder sb,
        FieldInfo field,
        string runtimeExpression,
        string payloadExpression)
    {
        var elementType = CollectionElementType(field.TypeFullName);
        var local = "__SpireCollection_" + Safe(field.Name);
        var index = "__SpireIndex_" + Safe(field.Name);
        var isArray = field.TypeFullName.EndsWith("[]", StringComparison.Ordinal);

        sb.AppendLine($"if ({runtimeExpression} is null)");
        sb.OpenBrace();
        sb.AppendLine($"{payloadExpression} = null!;");
        sb.CloseBrace();
        sb.AppendLine("else");
        sb.OpenBrace();
        if (isArray)
        {
            sb.AppendLine($"var {local} = {payloadExpression};");
            sb.AppendLine($"if ({local} is null || {local}.Length != {runtimeExpression}.Length || global::System.Object.ReferenceEquals({local}, {runtimeExpression})) {local} = new {elementType}[{runtimeExpression}.Length];");
            sb.AppendLine($"for (var {index} = 0; {index} < {runtimeExpression}.Length; {index}++)");
            sb.OpenBrace();
            sb.AppendLine($"var child = {runtimeExpression}[{index}];");
            sb.AppendLine($"var priorChild = {local} is null || {index} >= {local}.Length ? default({elementType})! : {local}[{index}];");
            sb.AppendLine($"child = global::Houtamelo.Spire.UnitySerializationBridge.Prepare(child, priorChild);");
            sb.AppendLine($"{local}![{index}] = child;");
            sb.CloseBrace();
            sb.AppendLine($"{payloadExpression} = {local};");
        }
        else
        {
            sb.AppendLine($"var {local} = {payloadExpression};");
            sb.AppendLine($"if ({local} is null || global::System.Object.ReferenceEquals({local}, {runtimeExpression})) {local} = new global::System.Collections.Generic.List<{elementType}>();");
            sb.AppendLine($"var {local}Count = {local}.Count;");
            sb.AppendLine($"for (var {index} = 0; {index} < {runtimeExpression}.Count; {index}++)");
            sb.OpenBrace();
            sb.AppendLine($"var child = {runtimeExpression}[{index}];");
            sb.AppendLine($"var priorChild = {index} < {local}Count ? {local}[{index}] : default({elementType})!;");
            sb.AppendLine($"child = global::Houtamelo.Spire.UnitySerializationBridge.Prepare(child, priorChild);");
            sb.AppendLine($"if ({index} < {local}.Count) {local}[{index}] = child; else {local}.Add(child);");
            sb.CloseBrace();
            sb.AppendLine($"while ({local}.Count > {runtimeExpression}.Count) {local}.RemoveAt({local}.Count - 1);");
            sb.AppendLine($"{payloadExpression} = {local};");
        }
        sb.CloseBrace();
    }

    private static string RestoreExpression(FieldInfo field, string expression, string variantName)
    {
        if (field.RequiresUnityRuntimeDispatch)
            return $"({field.TypeFullName})global::Houtamelo.Spire.UnitySerializationBridge.RestoreDynamic({expression})!";

        if (field.IsUnityUnion)
            return expression + ".__SpireHydrateSerialized()";

        if (!field.IsUnityUnionCollection)
        {
            return field.IsUnityComposite
                ? "global::Houtamelo.Spire.UnitySerializationBridge.Restore(" + expression + ")"
                : expression;
        }

        var helper = "__SpireHydrateCollection_" + Safe(field.Name) + "_" + Safe(variantName);
        return helper + "(" + expression + ")";
    }

    private static void EmitHydrationHelpers(SourceBuilder sb, UnionDeclaration union)
    {
        foreach (var variant in union.Variants)
        {
            foreach (var field in variant.Fields.Where(field => field.IsUnityUnionCollection))
            {
                var type = field.TypeFullName;
                var elementType = CollectionElementType(type);
                var helper = "__SpireHydrateCollection_" + Safe(field.Name) + "_" + Safe(variant.Name);
                var isArray = type.EndsWith("[]", StringComparison.Ordinal);
                sb.AppendLine($"private static {type} {helper}({type}? values)");
                sb.OpenBrace();
                sb.AppendLine("if (values is null) return null!;");
                if (isArray)
                {
                    sb.AppendLine($"var result = new {elementType}[values.Length];");
                    sb.AppendLine("for (var i = 0; i < values.Length; i++) result[i] = values[i].__SpireHydrateSerialized();");
                }
                else
                {
                    sb.AppendLine($"var result = new global::System.Collections.Generic.List<{elementType}>(values.Count);");
                    sb.AppendLine("for (var i = 0; i < values.Count; i++) result.Add(values[i].__SpireHydrateSerialized());");
                }
                sb.AppendLine("return result;");
                sb.CloseBrace();
                sb.AppendLine();
            }
        }
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
            sb.AppendLine("[global::System.Serializable]");
            sb.AppendLine($"private sealed class {PayloadName(variant.Name)} : {PayloadBaseName}");
            sb.OpenBrace();
            foreach (var field in variant.Fields)
                sb.AppendLine($"public {field.TypeFullName} {IdentifierUtility.Escape(field.Name)} = default!;");
            sb.CloseBrace();
            sb.AppendLine();
        }
    }

    private static string PayloadName(string variantName)
        => "__Spire" + Safe(variantName) + "Payload";

    private static string Safe(string name)
    {
        var raw = IdentifierUtility.SerializedName(name);
        var chars = raw.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        return chars.Length == 0 ? "Value" : new string(chars);
    }

    private static string CollectionElementType(string type)
    {
        if (type.EndsWith("[]", StringComparison.Ordinal))
            return type.Substring(0, type.Length - 2);

        const string prefix = "global::System.Collections.Generic.List<";
        if (type.StartsWith(prefix, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal))
            return type.Substring(prefix.Length, type.Length - prefix.Length - 1);

        var start = type.IndexOf('<');
        return start >= 0 && type.EndsWith(">", StringComparison.Ordinal)
            ? type.Substring(start + 1, type.Length - start - 2)
            : type;
    }
}
