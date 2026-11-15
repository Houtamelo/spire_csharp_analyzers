using System.Collections.Immutable;
using System.Composition;
using System.Collections.Generic;
using System;
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
public sealed class ReadonlyRecordVariantSerializationCodeFix : CodeFixProvider
{
    private const string EquivalenceKey = "Spire.RecordVariant.RemoveReadonly";

    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("SPIRE030");

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null || diagnostic.Id != "SPIRE030")
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (root is null || model is null ||
            !IsUserDocument(context.Document, root.SyntaxTree) ||
            !IsUnitySerializationCompilation(model.Compilation))
            return;

        if (!TryGetTargetRecord(context.Document, root, diagnostic, out var declaration) ||
            !IsReadonlyTokenTarget(declaration, diagnostic))
        {
            return;
        }

        var symbol = model.GetDeclaredSymbol(declaration, context.CancellationToken) as INamedTypeSymbol;
        if (symbol is null || !IsSupportedVariant(symbol))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove readonly from variant",
                cancellationToken => RemoveReadonlyAsync(
                    context.Document.Project.Solution,
                    symbol,
                    cancellationToken),
                EquivalenceKey),
            diagnostic);
    }

    private static async Task<Solution> RemoveReadonlyAsync(
        Solution solution,
        INamedTypeSymbol target,
        CancellationToken cancellationToken)
    {
        var updatedSolution = solution;
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
                    .Where(candidate => candidate.Modifiers.Any(
                        modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)))
                    .Where(candidate => SymbolEqualityComparer.Default.Equals(
                        model.GetDeclaredSymbol(candidate, cancellationToken),
                        target))
                    .ToArray();
                if (declarations.Length == 0)
                    continue;

                var changedRoot = root.ReplaceNodes(
                    declarations,
                    (declaration, _) => RemoveReadonlyModifier(declaration));

                if (!ReferenceEquals(changedRoot, root))
                    updatedSolution = updatedSolution.WithDocumentSyntaxRoot(document.Id, changedRoot);
            }
        }

        return updatedSolution;
    }

    private static RecordDeclarationSyntax RemoveReadonlyModifier(
        RecordDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (!modifiers[index].IsKind(SyntaxKind.ReadOnlyKeyword))
                continue;

            var token = modifiers[index];
            var trivia = token.LeadingTrivia.AddRange(token.TrailingTrivia);
            if (index + 1 < modifiers.Count)
            {
                if (index > 0 && EndsWithWhitespace(modifiers[index - 1].TrailingTrivia))
                    trivia = TrimLeadingWhitespace(trivia);

                var next = modifiers[index + 1]
                    .WithLeadingTrivia(trivia.AddRange(modifiers[index + 1].LeadingTrivia));
                modifiers = modifiers.Replace(modifiers[index + 1], next).RemoveAt(index);
            }
            else if (index > 0)
            {
                modifiers = modifiers.RemoveAt(index);
                if (EndsWithWhitespace(modifiers[index - 1].TrailingTrivia))
                    trivia = TrimLeadingWhitespace(trivia);
                var previous = modifiers[index - 1]
                    .WithTrailingTrivia(modifiers[index - 1].TrailingTrivia.AddRange(trivia));
                modifiers = modifiers.Replace(modifiers[index - 1], previous);
            }
            else
            {
                modifiers = modifiers.RemoveAt(index);
            }

            return declaration.WithModifiers(modifiers);
        }

        return declaration;
    }

    private static bool EndsWithWhitespace(SyntaxTriviaList trivia)
        => trivia.Count > 0 && trivia[trivia.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia);

    private static SyntaxTriviaList TrimLeadingWhitespace(SyntaxTriviaList trivia)
    {
        var first = 0;
        while (first < trivia.Count && trivia[first].IsKind(SyntaxKind.WhitespaceTrivia))
            first++;
        return SyntaxFactory.TriviaList(trivia.Skip(first));
    }

    private static bool TryGetTargetRecord(
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
        declaration = node.AncestorsAndSelf().OfType<RecordDeclarationSyntax>().FirstOrDefault()!;
        return declaration is not null;
    }

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

    private static bool IsReadonlyTokenTarget(
        RecordDeclarationSyntax declaration,
        Diagnostic diagnostic)
        => declaration.Modifiers.Any(modifier =>
            modifier.IsKind(SyntaxKind.ReadOnlyKeyword) &&
            modifier.Span == diagnostic.Location.SourceSpan);

    private static bool IsSupportedVariant(INamedTypeSymbol variant)
    {
        if (variant.TypeKind != TypeKind.Struct ||
            !variant.IsRecord ||
            variant.Arity != 0 ||
            variant.DeclaredAccessibility != Accessibility.Public ||
            !variant.IsReadOnly ||
            !HasAttribute(variant, "Houtamelo.Spire.VariantAttribute") ||
            variant.ContainingType is not INamedTypeSymbol union ||
            union.TypeKind != TypeKind.Struct ||
            union.IsRecord ||
            union.IsReadOnly ||
            union.IsRefLikeType ||
            !HasAttribute(union, "Houtamelo.Spire.DiscriminatedUnionAttribute") ||
            !HasAttribute(union, "System.SerializableAttribute"))
        {
            return false;
        }

        return variant.DeclaringSyntaxReferences
            .Where(reference => IsUserPath(reference.SyntaxTree.FilePath))
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .Any(candidate => candidate.ParameterList is { Parameters.Count: > 0 });
    }

    private static bool IsUnitySerializationCompilation(Compilation compilation)
        => compilation.SyntaxTrees.Any(tree =>
            tree.Options is CSharpParseOptions options &&
            options.PreprocessorSymbolNames.Contains("UNITY_5_3_OR_NEWER", StringComparer.Ordinal)) &&
            compilation.GetTypeByMetadataName("UnityEngine.ISerializationCallbackReceiver") is not null &&
            compilation.GetTypeByMetadataName("UnityEngine.SerializeField") is not null &&
            compilation.GetTypeByMetadataName("UnityEngine.SerializeReference") is not null;

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute => string.Equals(
            attribute.AttributeClass?.ToDisplayString(),
            metadataName,
            StringComparison.Ordinal));

    private static bool IsUserDocument(Document document, SyntaxTree syntaxTree)
        => IsUserPath(
            string.IsNullOrEmpty(document.FilePath)
                ? string.IsNullOrEmpty(syntaxTree.FilePath)
                    ? document.Name
                    : syntaxTree.FilePath
                : document.FilePath);

    private static bool IsUserPath(string? path)
        => path is null ||
            path.Length == 0 ||
            !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
}
