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
        });

        var unions = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Spire.DiscriminatedUnionAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.Parse(ctx, ct)
        ).Where(static u => u is not null);

        context.RegisterSourceOutput(unions, static (ctx, union) =>
        {
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

            var source = Emit(union);
            // Include arity to avoid collisions: Option vs Option<T>
            var arity = union.TypeParameters.Length > 0
                ? $"`{union.TypeParameters.Length}"
                : "";
            ctx.AddSource($"{union.TypeName}{arity}.g.cs", source);
        });
    }

    private static string Emit(UnionDeclaration union) => union.Strategy switch
    {
        EmitStrategy.Record => RecordEmitter.Emit(union),
        EmitStrategy.Class => ClassEmitter.Emit(union),
        EmitStrategy.Overlap => OverlapEmitter.Emit(union),
        EmitStrategy.BoxedFields => BoxedFieldsEmitter.Emit(union),
        EmitStrategy.BoxedTuple => BoxedTupleEmitter.Emit(union),
        _ => "// Unknown strategy",
    };
}
