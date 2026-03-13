using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE002MustBeInitOnFieldlessTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE002_MustBeInitOnFieldlessType);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.Analyzers.MustBeInitAttribute");

            if (mustBeInitType is null)
                return;

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, mustBeInitType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol mustBeInitType)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Struct)
            return;

        // Find the [MustBeInit] attribute on this type
        AttributeData? mustBeInitAttr = null;
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, mustBeInitType))
            {
                mustBeInitAttr = attr;
                break;
            }
        }

        if (mustBeInitAttr is null)
            return;

        if (MustBeInitChecks.HasInstanceFields(type))
            return;

        // Determine location: prefer the attribute application syntax, fall back to type declaration
        Location location;
        var attrSyntax = mustBeInitAttr.ApplicationSyntaxReference?.GetSyntax();
        if (attrSyntax != null)
            location = attrSyntax.GetLocation();
        else
            location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE002_MustBeInitOnFieldlessType,
                location,
                type.Name));
    }
}
