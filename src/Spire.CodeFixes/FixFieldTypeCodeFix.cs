using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class FixFieldTypeCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE011");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var expectedType = diagnostic.Properties.GetValueOrDefault("ExpectedType");
        if (expectedType is null or { Length: 0 }) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Change type to '{expectedType}'",
                createChangedDocument: ct => FixTypeAsync(context.Document, diagnostic, expectedType, ct),
                equivalenceKey: "FixFieldType"),
            diagnostic);
    }

    private static async Task<Document> FixTypeAsync(
        Document document, Diagnostic diagnostic, string expectedType, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        // Find the node at the diagnostic location
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Walk to find the DeclarationPatternSyntax
        var declPattern = node.AncestorsAndSelf()
            .OfType<DeclarationPatternSyntax>()
            .FirstOrDefault();
        if (declPattern is null) return document;

        // Replace the Type
        var newType = SyntaxFactory.ParseTypeName(expectedType)
            .WithTriviaFrom(declPattern.Type);
        var newPattern = declPattern.WithType(newType);

        var newRoot = root.ReplaceNode(declPattern, newPattern);
        return document.WithSyntaxRoot(newRoot);
    }
}
