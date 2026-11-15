using System;
using System.Collections.Generic;
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

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class RemoveRecordFromUnionCodeFix : CodeFixProvider
{
    private const string EquivalenceKey = "Spire.Union.RemoveRecord";

    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("SPIRE_DU020");

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null || diagnostic.Id != "SPIRE_DU020")
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (root is null || model is null || !IsUserDocument(context.Document, root.SyntaxTree))
            return;

        if (!TryGetTargetDeclaration(context.Document, root, diagnostic, out var declaration))
            return;

        var union = model.GetDeclaredSymbol(declaration, context.CancellationToken) as INamedTypeSymbol;
        if (union is null)
            return;

        var group = GetUnionGroup(union);
        if (group.Length == 0 || !group.Any(candidate => IsDiscriminatedUnion(candidate, model.Compilation)))
            return;

        if (HasNonEmptyOuterParameterList(group))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove record from union",
                cancellationToken => RemoveRecordAsync(
                    context.Document.Project.Solution,
                    group,
                    cancellationToken),
                EquivalenceKey),
            diagnostic);
    }

    private static async Task<Solution> RemoveRecordAsync(
        Solution solution,
        ImmutableArray<INamedTypeSymbol> targetGroup,
        CancellationToken cancellationToken)
    {
        var updated = solution;
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var model = await document.GetSemanticModelAsync(cancellationToken);
                if (root is null || model is null || !IsUserDocument(document, root.SyntaxTree))
                    continue;

                var declarations = root.DescendantNodes()
                    .OfType<RecordDeclarationSyntax>()
                    .Where(IsRecordStructDeclaration)
                    .Where(candidate => IsGroupMember(
                        model.GetDeclaredSymbol(candidate, cancellationToken),
                        targetGroup))
                    .ToArray();
                if (declarations.Length == 0)
                    continue;

                var changed = root.ReplaceNodes(
                    declarations,
                    (declaration, _) => ConvertToStruct(declaration));
                if (!ReferenceEquals(changed, root))
                    updated = updated.WithDocumentSyntaxRoot(document.Id, changed);
            }
        }

        return updated;
    }

    private static StructDeclarationSyntax ConvertToStruct(RecordDeclarationSyntax declaration)
    {
        var structKeyword = declaration.ClassOrStructKeyword
            .WithLeadingTrivia(
                declaration.Keyword.LeadingTrivia
                    .AddRange(declaration.Keyword.TrailingTrivia)
                    .AddRange(declaration.ClassOrStructKeyword.LeadingTrivia))
            .WithTrailingTrivia(declaration.ClassOrStructKeyword.TrailingTrivia);

        var identifier = declaration.Identifier;
        if (declaration.ParameterList is { Parameters.Count: 0 } parameters)
        {
            var parameterTrivia = parameters.OpenParenToken.LeadingTrivia
                .AddRange(parameters.OpenParenToken.TrailingTrivia)
                .AddRange(parameters.CloseParenToken.LeadingTrivia)
                .AddRange(parameters.CloseParenToken.TrailingTrivia);
            identifier = identifier.WithTrailingTrivia(
                identifier.TrailingTrivia.AddRange(parameterTrivia));
        }

        var openBrace = declaration.OpenBraceToken;
        var closeBrace = declaration.CloseBraceToken;
        var semicolon = declaration.SemicolonToken;
        if (openBrace.IsKind(SyntaxKind.None))
        {
            openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                .WithLeadingTrivia(semicolon.LeadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space);
            closeBrace = SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(semicolon.TrailingTrivia);
            semicolon = SyntaxFactory.Token(SyntaxKind.None);
        }

        var converted = SyntaxFactory.StructDeclaration(
                declaration.AttributeLists,
                declaration.Modifiers,
                structKeyword,
                identifier,
                declaration.TypeParameterList,
                declaration.BaseList,
                declaration.ConstraintClauses,
                openBrace,
                declaration.Members,
                closeBrace,
                semicolon);
        return converted;
    }

    private static bool TryGetTargetDeclaration(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        out RecordDeclarationSyntax declaration)
    {
        declaration = null!;
        if (!diagnostic.Location.IsInSource ||
            diagnostic.Location.SourceTree is null ||
            !SourceTreesMatch(document, root.SyntaxTree, diagnostic.Location.SourceTree) ||
            diagnostic.Location.SourceSpan.End > root.FullSpan.End)
        {
            return false;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        declaration = node.AncestorsAndSelf()
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault(candidate =>
                IsRecordStructDeclaration(candidate) &&
                candidate.Keyword.Span == diagnostic.Location.SourceSpan)!;
        return declaration is not null;
    }

    private static bool IsRecordStructDeclaration(RecordDeclarationSyntax declaration)
        => declaration.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);

    private static bool IsDiscriminatedUnion(INamedTypeSymbol symbol, Compilation compilation)
    {
        var attribute = compilation.GetTypeByMetadataName(
            "Houtamelo.Spire.DiscriminatedUnionAttribute");
        return attribute is not null && symbol.GetAttributes().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute));
    }

    private static ImmutableArray<INamedTypeSymbol> GetUnionGroup(INamedTypeSymbol symbol)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        Add(symbol);

        var siblings = symbol.ContainingType is { } containing
            ? containing.GetTypeMembers(symbol.Name, symbol.Arity)
            : symbol.ContainingNamespace.GetTypeMembers(symbol.Name, symbol.Arity);
        foreach (var sibling in siblings)
            Add(sibling);

        return builder.ToImmutable();

        void Add(INamedTypeSymbol candidate)
        {
            if (!candidate.IsValueType || candidate.TypeKind != TypeKind.Struct ||
                builder.Any(existing => SymbolEqualityComparer.Default.Equals(existing, candidate)))
            {
                return;
            }

            builder.Add(candidate);
        }
    }

    private static bool IsGroupMember(
        INamedTypeSymbol? candidate,
        ImmutableArray<INamedTypeSymbol> group)
        => candidate is not null && group.Any(member =>
            SymbolEqualityComparer.Default.Equals(member, candidate));

    private static bool HasNonEmptyOuterParameterList(
        ImmutableArray<INamedTypeSymbol> group)
        => group.SelectMany(union => union.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Where(declaration => IsUserPath(declaration.SyntaxTree.FilePath))
            .Any(declaration => declaration switch
            {
                RecordDeclarationSyntax record => record.ParameterList is { Parameters.Count: > 0 },
                _ => false,
            });

    private static bool SourceTreesMatch(
        Document document,
        SyntaxTree documentTree,
        SyntaxTree diagnosticTree)
    {
        if (ReferenceEquals(documentTree, diagnosticTree))
            return true;

        var documentPath = string.IsNullOrEmpty(documentTree.FilePath)
            ? document.Name
            : documentTree.FilePath;
        var diagnosticPath = diagnosticTree.FilePath;
        return !string.IsNullOrEmpty(diagnosticPath) &&
            string.Equals(documentPath, diagnosticPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserDocument(Document document, SyntaxTree tree)
        => IsUserPath(
            string.IsNullOrEmpty(document.FilePath)
                ? string.IsNullOrEmpty(tree.FilePath) ? document.Name : tree.FilePath
                : document.FilePath);

    private static bool IsUserPath(string? path)
        => path is null || path.Length == 0 ||
            !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
}
