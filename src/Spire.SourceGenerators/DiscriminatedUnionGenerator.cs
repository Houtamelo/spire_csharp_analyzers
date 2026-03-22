using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spire.SourceGenerators.Attributes;
using Spire.SourceGenerators.Emit;
using Spire.SourceGenerators.Model;
using Spire.SourceGenerators.Parsing;

namespace Spire.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class DiscriminatedUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(AttributeSource.Hint_DiscriminatedUnion,
                AttributeSource.DiscriminatedUnionAttribute);
            ctx.AddSource(AttributeSource.Hint_Variant,
                AttributeSource.VariantAttribute);
            ctx.AddSource(AttributeSource.Hint_Layout,
                AttributeSource.LayoutEnum);
            ctx.AddSource(AttributeSource.Hint_JsonLibrary,
                AttributeSource.JsonLibraryEnum);
            ctx.AddSource(AttributeSource.Hint_JsonName,
                AttributeSource.JsonNameAttribute);
        });

        var unions = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Spire.DiscriminatedUnionAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.Parse(ctx, ct)
        ).Where(static u => u is not null);

        var compilationInfo = context.CompilationProvider.Select(static (comp, _) =>
            new CompilationInfo(
                HasSystemTextJson: comp.GetTypeByMetadataName(
                    "System.Text.Json.Serialization.JsonConverter`1") is not null,
                HasNewtonsoftJson: comp.GetTypeByMetadataName(
                    "Newtonsoft.Json.JsonConverter") is not null,
                AllowsUnsafe: ((Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)comp.Options).AllowUnsafe,
                HasInlineArray: comp.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.InlineArrayAttribute") is not null));

        var combined = unions.Combine(compilationInfo);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (union, compInfo) = pair;
            if (union is null) return;

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
                    Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, location));

                // Don't emit source for error diagnostics
                if (diag.IsError) return;
            }

            // Check for field name+type conflicts across variants (always an error)
            if (HasFieldNameConflicts(ctx, union))
                return;

            // UnsafeOverlap requires AllowUnsafe
            if (union.Strategy == EmitStrategy.UnsafeOverlap && !compInfo.AllowsUnsafe)
            {
                ReportJsonDiagnostic(ctx, union,
                    "SPIRE_DU009",
                    "UnsafeOverlap layout requires <AllowUnsafeBlocks>true</AllowUnsafeBlocks> in the project");
                return;
            }

            var source = union.Strategy == EmitStrategy.UnsafeOverlap
                ? UnsafeOverlapEmitter.Emit(union, compInfo.HasInlineArray)
                : Emit(union);
            // Include arity to avoid collisions: Option vs Option<T>
            var arity = union.TypeParameters.Length > 0
                ? $"`{union.TypeParameters.Length}"
                : "";
            var hintPrefix = $"{union.TypeName}{arity}";
            ctx.AddSource($"{hintPrefix}.g.cs", source);

            // JSON: System.Text.Json
            if ((union.Json & JsonLibrary.SystemTextJson) != 0)
            {
                if (!compInfo.HasSystemTextJson)
                {
                    ReportJsonDiagnostic(ctx, union,
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
                    ReportJsonDiagnostic(ctx, union,
                        "SPIRE_DU007",
                        "Newtonsoft.Json not referenced but Json includes NewtonsoftJson");
                }
                else
                {
                    var nsjSource = NewtonsoftJsonEmitter.Emit(union);
                    ctx.AddSource($"{hintPrefix}.Nsj.g.cs", nsjSource);
                }
            }
        });
    }

    private static void ReportJsonDiagnostic(
        SourceProductionContext ctx, UnionDeclaration union,
        string id, string message)
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
        EmitStrategy.Class => ClassEmitter.Emit(union),
        EmitStrategy.Overlap => OverlapEmitter.Emit(union),
        EmitStrategy.BoxedFields => BoxedFieldsEmitter.Emit(union),
        EmitStrategy.BoxedTuple => BoxedTupleEmitter.Emit(union),
        _ => "// Unknown strategy",
    };
}
