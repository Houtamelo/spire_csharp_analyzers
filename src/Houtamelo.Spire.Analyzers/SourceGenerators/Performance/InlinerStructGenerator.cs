using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

[Generator(LanguageNames.CSharp)]
public sealed class InlinerStructGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Houtamelo.Spire.InlinerStructAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var decls = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeFullName,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, ct) => InlinerStructParser.Parse(ctx, ct))
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        context.RegisterSourceOutput(decls, static (srcCtx, decl) =>
        {
            if (decl.Diagnostic is { } diag)
            {
                srcCtx.ReportDiagnostic(InlinerDiagnosticFactory.CreateDiagnostic(diag, ResolveDescriptor(diag.Id)));
                return;
            }

            var source = InlinerStructEmitter.Emit(decl);
            srcCtx.AddSource(decl.HintName, source);
        });
    }


    private static DiagnosticDescriptor? ResolveDescriptor(string id)
        => id switch
        {
            "SPIRE017" => InlinerDescriptors.SPIRE017_UnsupportedParameterModifier,
            "SPIRE018" => InlinerDescriptors.SPIRE018_RefStructDeclaringType,
            "SPIRE019" => InlinerDescriptors.SPIRE019_ArityExceeded,
            "SPIRE020" => InlinerDescriptors.SPIRE020_NameCollision,
            "SPIRE027" => InlinerDescriptors.SPIRE027_EnclosingTypeNotPartial,
            _ => null,
        };
}
