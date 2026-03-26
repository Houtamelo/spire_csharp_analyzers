using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE002EnforceInitializationOnFieldlessTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE002_EnforceInitializationOnFieldlessType);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.Core.EnforceInitializationAttribute");

            if (enforceInitializationType is null)
                return;

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, enforceInitializationType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol enforceInitializationType)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Class)
            return;

        // Find the [EnforceInitialization] attribute on this type
        AttributeData? enforceInitializationAttr = null;
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforceInitializationType))
            {
                enforceInitializationAttr = attr;
                break;
            }
        }

        if (enforceInitializationAttr is null)
            return;

        if (EnforceInitializationChecks.HasInstanceFields(type))
            return;

        // Determine location: prefer the attribute application syntax, fall back to type declaration
        Location location;
        var attrSyntax = enforceInitializationAttr.ApplicationSyntaxReference?.GetSyntax();
        if (attrSyntax != null)
            location = attrSyntax.GetLocation();
        else
            location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE002_EnforceInitializationOnFieldlessType,
                location,
                type.Name));
    }
}
