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
                srcCtx.ReportDiagnostic(CreateDiagnostic(diag));
                return;
            }

            var source = InlinerStructEmitter.Emit(decl);
            var hint = $"{decl.DeclaringTypeName}.{decl.MethodName}.InlinerStruct.g.cs";
            srcCtx.AddSource(hint, source);
        });
    }

    private static Diagnostic CreateDiagnostic(InlinerDiagnostic d)
    {
        var location = string.IsNullOrEmpty(d.FilePath)
            ? Location.None
            : Location.Create(
                d.FilePath,
                new Microsoft.CodeAnalysis.Text.TextSpan(d.StartOffset, d.Length),
                new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                    new Microsoft.CodeAnalysis.Text.LinePosition(d.StartLine, d.StartColumn),
                    new Microsoft.CodeAnalysis.Text.LinePosition(d.EndLine, d.EndColumn)));

        var descriptor = ResolveDescriptor(d.Id)
            ?? new DiagnosticDescriptor(
                d.Id, d.Id, d.Message, "SourceGeneration",
                DiagnosticSeverity.Error, isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, location);
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
