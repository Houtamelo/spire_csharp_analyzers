using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

[Generator(LanguageNames.CSharp)]
public sealed class InlinableTwinGenerator : IIncrementalGenerator
{
    private const string InlinableAttrFullName = "Houtamelo.Spire.InlinableAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var decls = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is MethodDeclarationSyntax mds
                && mds.ParameterList.Parameters.Any(p => p.AttributeLists.Count > 0),
            transform: static (ctx, ct) =>
            {
                var mds = (MethodDeclarationSyntax)ctx.Node;
                if (ctx.SemanticModel.GetDeclaredSymbol(mds, ct) is not IMethodSymbol symbol)
                    return null;
                var inlinableAttr = ctx.SemanticModel.Compilation
                    .GetTypeByMetadataName(InlinableAttrFullName);
                if (inlinableAttr is null)
                    return null;
                var hasAny = symbol.Parameters.Any(p => p.GetAttributes()
                    .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, inlinableAttr)));
                if (!hasAny)
                    return null;
                return InlinableParser.Parse(symbol, mds, ctx.SemanticModel, ct);
            })
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        context.RegisterSourceOutput(decls, static (srcCtx, decl) =>
        {
            if (decl.Diagnostic is { } diag)
            {
                srcCtx.ReportDiagnostic(CreateDiagnostic(diag));
                return;
            }

            var source = InlinableTwinEmitter.Emit(decl);
            var hint = $"{decl.DeclaringTypeName}.{decl.MethodName}.InlinableTwin.g.cs";
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

        var template = ResolveDescriptor(d.Id);
        var descriptor = template is null
            ? new DiagnosticDescriptor(
                d.Id, d.Id, d.Message, "SourceGeneration",
                DiagnosticSeverity.Error, isEnabledByDefault: true)
            : new DiagnosticDescriptor(
                template.Id,
                template.Title,
                d.Message,
                template.Category,
                template.DefaultSeverity,
                template.IsEnabledByDefault);

        return Diagnostic.Create(descriptor, location);
    }

    private static DiagnosticDescriptor? ResolveDescriptor(string id)
        => id switch
        {
            "SPIRE021" => InlinerDescriptors.SPIRE021_UnsupportedBodyUsage,
            "SPIRE022" => InlinerDescriptors.SPIRE022_NonDelegateParameter,
            "SPIRE023" => InlinerDescriptors.SPIRE023_ContainerNotPartial,
            "SPIRE024" => InlinerDescriptors.SPIRE024_DelegateArityExceeded,
            "SPIRE025" => InlinerDescriptors.SPIRE025_UnsupportedRefKind,
            "SPIRE026" => InlinerDescriptors.SPIRE026_PropertyOrIndexerParameter,
            "SPIRE027" => InlinerDescriptors.SPIRE027_EnclosingTypeNotPartial,
            _ => null,
        };
}
