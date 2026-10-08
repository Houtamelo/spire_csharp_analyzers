using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class DiscriminatedUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var unions = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Houtamelo.Spire.DiscriminatedUnionAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.Parse(ctx, ct)
        ).Where(static u => u is not null);

        var globalConfig = context.AnalyzerConfigOptionsProvider.Select(
            static (provider, _) => SpireGlobalConfig.Parse(provider.GlobalOptions));

        var compilationInfo = context.CompilationProvider.Select(static (comp, _) =>
        {
            var parseOptions = comp.SyntaxTrees
                .Select(tree => tree.Options)
                .OfType<CSharpParseOptions>()
                .FirstOrDefault();
            var supportsInlineArraySyntax = parseOptions is not null &&
                parseOptions.LanguageVersion >= LanguageVersion.CSharp12;

            return new CompilationInfo(
                HasSystemTextJson: comp.GetTypeByMetadataName(
                    "System.Text.Json.Serialization.JsonConverter`1") is not null,
                HasNewtonsoftJson: comp.GetTypeByMetadataName(
                    "Newtonsoft.Json.JsonConverter") is not null,
                AllowsUnsafe: ((Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)comp.Options).AllowUnsafe,
                HasInlineArray: comp.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.InlineArrayAttribute") is not null &&
                    supportsInlineArraySyntax,
                HasInitProperties: CompilationInfo.HasAccessibleType(
                    comp, "System.Runtime.CompilerServices.IsExternalInit"));
        });

        var combined = unions
            .Combine(globalConfig)
            .Combine(compilationInfo);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var ((union, config), compInfo) = pair;
            if (union is null) return;

            // Resolve global config sentinels
            union = UnionParser.ResolveGlobalConfig(union, config);
            union = union with { HasInitProperties = compInfo.HasInitProperties };

            // Report diagnostic if present
            if (union.Diagnostic is { } diag)
            {
                var descriptor = Diagnostics.GetDescriptor(diag);
                var location = Location.Create(
                    diag.FilePath,
                    new Microsoft.CodeAnalysis.Text.TextSpan(diag.StartOffset, diag.Length),
                    new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                        new Microsoft.CodeAnalysis.Text.LinePosition(diag.StartLine, diag.StartColumn),
                        new Microsoft.CodeAnalysis.Text.LinePosition(diag.EndLine, diag.EndColumn)));
                ctx.ReportDiagnostic(
                    descriptor.MessageFormat.ToString().Contains("{0}")
                        ? Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, location, diag.Message)
                        : Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, location));

                // Don't emit source for error diagnostics
                if (diag.IsError) return;
            }

            // Post-resolution validation: Overlap on generic struct (e.g., via global config)
            if (union.Strategy == EmitStrategy.Overlap &&
                (union.IsGeneric || union.ContainingTypes.Any(type => type.TypeParameters.Length > 0)))
            {
                ReportDiagnostic(ctx,
                    "SPIRE_DU005",
                    "Generic structs and unions nested in generic containing types cannot use Overlap layout (CLR restriction); use BoxedFields or BoxedTuple");
                return;
            }

            // Check for field name+type conflicts across variants (always an error)
            if (HasFieldNameConflicts(ctx, union))
                return;

            // UnsafeOverlap requires AllowUnsafe
            if (union.Strategy == EmitStrategy.UnsafeOverlap && !compInfo.AllowsUnsafe)
            {
                ReportDiagnostic(ctx,
                    "SPIRE_DU009",
                    "UnsafeOverlap layout requires <AllowUnsafeBlocks>true</AllowUnsafeBlocks> in the project");
                return;
            }

            var source = union.IsUnitySerializable
                ? UnitySerializationEmitter.Emit(union, compInfo.HasInlineArray)
                : union.Strategy == EmitStrategy.UnsafeOverlap
                ? UnsafeOverlapEmitter.Emit(union, compInfo.HasInlineArray)
                : Emit(union);
            // Include arity to avoid collisions: Option vs Option<T>
            var arity = union.TypeParameters.Length > 0
                ? $"`{union.TypeParameters.Length}"
                : "";
            var hintPrefix = union.IsUnitySerializable
                ? UnityHintPrefix(union, arity)
                : $"{union.TypeName}{arity}";
            ctx.AddSource($"{hintPrefix}.g.cs", source);

            if (union.IsUnitySerializable)
                ctx.AddSource(
                    $"{hintPrefix}.UnityDrawer.g.cs",
                    UnityPropertyDrawerEmitter.Emit(union));

            // ToString (record only — struct ToString is inline)
            if (union.Strategy == EmitStrategy.Record)
            {
                var toStringSource = ToStringEmitter.EmitRecordToString(union);
                ctx.AddSource($"{hintPrefix}.ToString.g.cs", toStringSource);
            }

            // JSON: System.Text.Json
            if ((union.Json & JsonLibrary.SystemTextJson) != 0)
            {
                if (!compInfo.HasSystemTextJson)
                {
                    ReportDiagnostic(ctx,
                        "SPIRE_DU006",
                        "System.Text.Json not referenced but Json includes SystemTextJson");
                }
                else
                {
                    var stjSource = SystemTextJsonEmitter.Emit(union);
                    ctx.AddSource($"{hintPrefix}.Stj.g.cs", stjSource);
                }
            }

            // JSON: Newtonsoft.Json
            if ((union.Json & JsonLibrary.NewtonsoftJson) != 0)
            {
                if (!compInfo.HasNewtonsoftJson)
                {
                    ReportDiagnostic(ctx,
                        "SPIRE_DU007",
                        "Newtonsoft.Json not referenced but Json includes NewtonsoftJson");
                }
                else
                {
                    var nsjSource = NewtonsoftJsonEmitter.Emit(union);
                    ctx.AddSource($"{hintPrefix}.Nsj.g.cs", nsjSource);
                }
            }

            // JSON Schema
            if (union.Json != JsonLibrary.None)
            {
                bool hasStj = (union.Json & JsonLibrary.SystemTextJson) != 0 && compInfo.HasSystemTextJson;
                var schemaSource = JsonSchemaEmitter.Emit(union, hasStj);
                ctx.AddSource($"{hintPrefix}.Schema.g.cs", schemaSource);
            }
        });
    }

    private static void ReportDiagnostic(
        SourceProductionContext ctx, string id, string message)
    {
        var diag = new UnionDiagnostic(
            Id: id, Message: message, IsError: true,
            FilePath: "", StartOffset: 0, Length: 0,
            StartLine: 0, StartColumn: 0, EndLine: 0, EndColumn: 0);
        var descriptor = Diagnostics.GetDescriptor(diag);
        ctx.ReportDiagnostic(
            Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, Location.None));
    }

    /// Returns true if field name conflicts exist (same name, different type).
    /// Reports SPIRE_DU010 for each conflict and prevents source emission.
    private static bool HasFieldNameConflicts(SourceProductionContext ctx, UnionDeclaration union)
    {
        // Collect all (name → set of types) across all variants
        var fieldTypes = new Dictionary<string, HashSet<string>>();
        foreach (var variant in union.Variants)
        {
            foreach (var field in variant.Fields)
            {
                if (!fieldTypes.TryGetValue(field.Name, out var types))
                {
                    types = new HashSet<string>();
                    fieldTypes[field.Name] = types;
                }
                types.Add(field.TypeFullName);
            }
        }

        bool hasConflict = false;
        foreach (var kv in fieldTypes)
        {
            if (kv.Value.Count <= 1) continue;

            hasConflict = true;
            var typeList = string.Join(", ", kv.Value.OrderBy(t => t));
            var message = $"Field '{kv.Key}' has conflicting types across variants: {typeList}. " +
                          $"Rename the field to resolve the conflict.";

            var diag = new UnionDiagnostic(
                Id: "SPIRE_DU010", Message: message, IsError: true,
                FilePath: "", StartOffset: 0, Length: 0,
                StartLine: 0, StartColumn: 0, EndLine: 0, EndColumn: 0);
            var descriptor = Diagnostics.GetDescriptor(diag);
            ctx.ReportDiagnostic(
                Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, Location.None, message));
        }

        return hasConflict;
    }

    private static string Emit(UnionDeclaration union) => union.Strategy switch
    {
        EmitStrategy.Additive => AdditiveEmitter.Emit(union),
        EmitStrategy.Record => RecordEmitter.Emit(union),
        EmitStrategy.Overlap => OverlapEmitter.Emit(union),
        EmitStrategy.BoxedFields => BoxedFieldsEmitter.Emit(union),
        EmitStrategy.BoxedTuple => BoxedTupleEmitter.Emit(union),
        _ => "// Unknown strategy",
    };

    private static string UnityHintPrefix(UnionDeclaration union, string arity)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(union.Namespace))
            parts.Add(union.Namespace);
        parts.AddRange(union.ContainingTypes.Select(type => type.Name));
        parts.Add(union.TypeName + arity);

        var text = string.Join(".", parts);
        return new string(text.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '_' or '`'
                ? character
                : '_').ToArray());
    }
}
