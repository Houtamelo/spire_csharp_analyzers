using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

internal static class InlinerDiagnosticFactory
{
    internal static Diagnostic CreateDiagnostic(InlinerDiagnostic d, DiagnosticDescriptor? template)
    {
        var location = string.IsNullOrEmpty(d.FilePath)
            ? Location.None
            : Location.Create(
                d.FilePath,
                new Microsoft.CodeAnalysis.Text.TextSpan(d.StartOffset, d.Length),
                new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                    new Microsoft.CodeAnalysis.Text.LinePosition(d.StartLine, d.StartColumn),
                    new Microsoft.CodeAnalysis.Text.LinePosition(d.EndLine, d.EndColumn)));

        // Use a per-call descriptor that ships the parser's already-formatted message
        // in the Title/MessageFormat slots. Keeps Id/Category/Severity from the template
        // so diagnostic filtering and release-tracking work uniformly.
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
}
