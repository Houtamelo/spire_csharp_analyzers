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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class AddMissingArmsCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE009", "SPIRE015");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (diagnostic.Id is not ("SPIRE009" or "SPIRE015"))
            return;

        var hasPatternMetadata = HasPatternMetadata(diagnostic);
        var hasExactPatterns = TryGetExactPatternTexts(diagnostic, out var exactPatterns);
        if (hasPatternMetadata && !hasExactPatterns)
            return;

        if (hasExactPatterns && exactPatterns.Any(pattern => ParsePattern(pattern) is null))
            return;

        var propertyName = "MissingVariants";
        var missingStr = diagnostic.Properties.GetValueOrDefault(propertyName);
        if (!hasExactPatterns &&
            (diagnostic.Id == "SPIRE015" ||
             diagnostic.Properties.GetValueOrDefault("UseVariantTemplates") != "true" ||
             missingStr is null or { Length: 0 }))
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null || FindSwitch(root, diagnostic) is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnostic.Id == "SPIRE015"
                    ? "Add missing enum members"
                    : "Add missing variant arms",
                createChangedDocument: ct => AddMissingArmsAsync(context.Document, diagnostic, ct),
                equivalenceKey: "AddMissingArms"),
            diagnostic);
    }

    private static async Task<Document> AddMissingArmsAsync(
        Document document, Diagnostic diagnostic, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        var formattingOptions = await document.GetOptionsAsync(ct);
        if (root is null || model is null) return document;

        // Newer analyzers provide complete C# pattern text for every missing
        // case. Preserve those patterns exactly; unlike a flat variant-name
        // list they can represent null, aliases, nested properties, and
        // correlated payload constraints.
        var hasPatternMetadata = HasPatternMetadata(diagnostic);
        if (hasPatternMetadata &&
            !TryGetExactPatternTexts(diagnostic, out _))
            return document;

        if (TryGetExactPatternTexts(diagnostic, out var exactPatterns))
        {
            return await ApplyExactPatternsAsync(
                document,
                root,
                formattingOptions,
                diagnostic,
                exactPatterns,
                ct);
        }

        // SPIRE015 is only fixable when the analyzer exports structured
        // patterns. A flat member-name list cannot distinguish enum members
        // such as @null from a nullable null arm.
        if (diagnostic.Id == "SPIRE015")
            return document;

        if (diagnostic.Properties.GetValueOrDefault("UseVariantTemplates") != "true")
            return document;

        var missingVariantsStr = diagnostic.Properties.GetValueOrDefault("MissingVariants");
        if (missingVariantsStr is null) return document;
        var missingVariants = missingVariantsStr.Split(',');

        var node = FindSwitch(root, diagnostic);
        if (node is null)
            return document;

        // Try switch expression
        var switchExpr = node as SwitchExpressionSyntax;
        if (switchExpr is not null)
        {
            var subjectType = model.GetTypeInfo(switchExpr.GoverningExpression, ct).Type
                as INamedTypeSymbol;
            subjectType = UnwrapNullableNamedType(subjectType);
            if (subjectType is null) return document;
            if (!IsDiscriminatedUnion(subjectType)) return document;

            return ApplySwitchExpression(
                document,
                root,
                switchExpr,
                subjectType,
                missingVariants,
                model,
                formattingOptions);
        }

        // Try switch statement
        var switchStmt = node as SwitchStatementSyntax;
        if (switchStmt is not null)
        {
            var subjectType = model.GetTypeInfo(switchStmt.Expression, ct).Type
                as INamedTypeSymbol;
            subjectType = UnwrapNullableNamedType(subjectType);
            if (subjectType is null) return document;
            if (!IsDiscriminatedUnion(subjectType)) return document;

            return ApplySwitchStatement(
                document,
                root,
                switchStmt,
                subjectType,
                missingVariants,
                model,
                formattingOptions);
        }

        return document;
    }

    private static bool HasPatternMetadata(Diagnostic diagnostic) =>
        diagnostic.Properties.ContainsKey("MissingPatternCount") &&
        diagnostic.Properties.GetValueOrDefault("UseVariantTemplates") != "true";

    private static INamedTypeSymbol? UnwrapNullableNamedType(INamedTypeSymbol? type)
    {
        if (type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return type.TypeArguments[0] as INamedTypeSymbol;
        return type;
    }

    private static bool IsDiscriminatedUnion(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
            "Houtamelo.Spire.DiscriminatedUnionAttribute");
    }

    private static SyntaxNode? FindSwitch(SyntaxNode root, Diagnostic diagnostic)
    {
        var span = diagnostic.Location.SourceSpan;
        if (span.Start < root.FullSpan.Start || span.End > root.FullSpan.End)
            return null;

        var node = root.FindNode(span, getInnermostNodeForTie: true);
        return node.AncestorsAndSelf()
            .FirstOrDefault(candidate =>
                candidate is SwitchExpressionSyntax or SwitchStatementSyntax);
    }

    private static bool TryGetExactPatternTexts(
        Diagnostic diagnostic,
        out string[] patterns)
    {
        patterns = System.Array.Empty<string>();
        if (diagnostic.Properties.GetValueOrDefault("UseVariantTemplates") == "true")
            return false;
        var countText = diagnostic.Properties.GetValueOrDefault("MissingPatternCount");
        if (!int.TryParse(countText, out var count) || count <= 0 ||
            count > diagnostic.Properties.Count)
            return false;

        var values = new string[count];
        for (var index = 0; index < count; index++)
        {
            var value = diagnostic.Properties.GetValueOrDefault($"MissingPattern{index}");
            if (string.IsNullOrWhiteSpace(value))
                return false;
            values[index] = value!;
        }

        patterns = values;
        return true;
    }

    private static async Task<Document> ApplyExactPatternsAsync(
        Document document,
        SyntaxNode root,
        OptionSet formattingOptions,
        Diagnostic diagnostic,
        string[] patternTexts,
        CancellationToken ct)
    {
        var node = FindSwitch(root, diagnostic);
        if (node is null)
            return document;
        var sourceLineEnding = GetLineEnding(
            (await document.GetTextAsync(ct)).ToString());
        var parsedPatterns = new List<PatternSyntax>(patternTexts.Length);
        foreach (var text in patternTexts)
        {
            var pattern = ParsePattern(text);
            if (pattern is null)
                return document;
            parsedPatterns.Add(pattern);
        }

        var switchExpr = node as SwitchExpressionSyntax;
        if (switchExpr is not null)
        {
            var leadingTrivia = GetArmLeadingTrivia(switchExpr, formattingOptions);
            var arms = parsedPatterns
                .Select(pattern => SyntaxFactory.SwitchExpressionArm(
                        pattern,
                        ThrowNotImplementedExpr())
                    .WithLeadingTrivia(leadingTrivia))
                .ToArray();
            var updatedSwitch = AppendExpressionArms(
                switchExpr,
                arms,
                sourceLineEnding,
                leadingTrivia);
            var changedDocument = document.WithSyntaxRoot(
                root.ReplaceNode(switchExpr, updatedSwitch));
            var simplifiedDocument = await Simplifier.ReduceAsync(
                changedDocument,
                Simplifier.Annotation,
                cancellationToken: ct);
            return await NormalizeSwitchLineEndingsAsync(
                simplifiedDocument,
                diagnostic,
                sourceLineEnding,
                parsedPatterns.Count,
                ct);
        }

        var switchStmt = node as SwitchStatementSyntax;
        if (switchStmt is null)
            return document;

        var sections = parsedPatterns
            .Select(BuildExactSection)
            .ToArray();
        var updatedStatement = AppendSections(switchStmt, sections, formattingOptions);

        var changedStatementDocument = document.WithSyntaxRoot(
            root.ReplaceNode(switchStmt, updatedStatement));
        var simplifiedStatementDocument = await Simplifier.ReduceAsync(
            changedStatementDocument,
            Simplifier.Annotation,
            cancellationToken: ct);
        return await NormalizeSwitchLineEndingsAsync(
            simplifiedStatementDocument,
            diagnostic,
            sourceLineEnding,
            parsedPatterns.Count,
            ct);
    }

    private static async Task<Document> NormalizeSwitchLineEndingsAsync(
        Document document,
        Diagnostic diagnostic,
        string lineEnding,
        int addedPatternCount,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null)
            return document;

        var switchNode = FindSwitch(root, diagnostic);
        var normalizedDocument = document;
        if (switchNode is not null)
        {
            if (switchNode is SwitchExpressionSyntax switchExpression &&
                addedPatternCount > 0 &&
                addedPatternCount <= switchExpression.Arms.Count)
            {
                var formattedExpression = FormatAddedExpressionArms(
                    switchExpression,
                    addedPatternCount,
                    lineEnding);
                normalizedDocument = document.WithSyntaxRoot(
                    root.ReplaceNode(switchNode, formattedExpression));
                var refreshedRoot = await normalizedDocument.GetSyntaxRootAsync(ct);
                if (refreshedRoot is null)
                    return normalizedDocument;
                root = refreshedRoot;
                switchNode = FindSwitch(root, diagnostic);
            }

            if (switchNode is not null)
            {
                var endOfLines = switchNode.DescendantTrivia()
                    .Where(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    .ToArray();
                if (endOfLines.Length > 0)
                {
                    var normalizedSwitch = switchNode.ReplaceTrivia(
                        endOfLines,
                        (_, _) => SyntaxFactory.EndOfLine(lineEnding));
                    normalizedDocument = normalizedDocument.WithSyntaxRoot(
                        root.ReplaceNode(switchNode, normalizedSwitch));
                }
            }
        }
        // Materialize the formatted, simplified syntax as plain source text
        // in the original document. This drops simplifier/formatter
        // annotations and prevents a later workspace cleanup from replacing
        // the explicitly selected source line ending on the new subtree.
        var text = await normalizedDocument.GetTextAsync(ct);
        return document.WithText(SourceText.From(text.ToString(), text.Encoding));
    }

    private static SwitchExpressionSyntax FormatAddedExpressionArms(
        SwitchExpressionSyntax switchExpr,
        int addedCount,
        string lineEnding)
    {
        var existingCount = switchExpr.Arms.Count - addedCount;
        var armLineLeading = switchExpr.Arms.FirstOrDefault() is { } existingArm
            ? GetTokenLineLeadingTrivia(existingArm.GetFirstToken())
            : SyntaxFactory.ParseLeadingTrivia(
                lineEnding + GetTrailingWhitespace(
                    switchExpr.CloseBraceToken.LeadingTrivia));
        var nodes = switchExpr.Arms.GetWithSeparators().ToList();
        var hasTrailingComma = nodes.Count > 0 && nodes[nodes.Count - 1].IsToken;
        var separatorCount = hasTrailingComma
            ? switchExpr.Arms.Count
            : switchExpr.Arms.Count - 1;
        if (existingCount > 0 && existingCount - 1 < separatorCount)
        {
            var precedingSeparatorIndex = (existingCount - 1) * 2 + 1;
            var precedingSeparator = nodes[precedingSeparatorIndex].AsToken();
            if (!HasCommentTrivia(precedingSeparator.TrailingTrivia))
            {
                nodes[precedingSeparatorIndex] = precedingSeparator
                    .WithTrailingTrivia(default(SyntaxTriviaList));
            }
        }
        for (var index = existingCount; index < switchExpr.Arms.Count; index++)
        {
            var arm = switchExpr.Arms[index]
                .NormalizeWhitespace(elasticTrivia: false)
                .WithLeadingTrivia(
                    index == existingCount &&
                    HasCommentTrivia(switchExpr.Arms[index].GetFirstToken().LeadingTrivia)
                        ? switchExpr.Arms[index].GetFirstToken().LeadingTrivia
                        : armLineLeading);
            nodes[index * 2] = arm;

            if (index >= switchExpr.Arms.Count - 1 || index >= separatorCount)
                continue;

            nodes[index * 2 + 1] = nodes[index * 2 + 1].AsToken()
                .WithTrailingTrivia(default(SyntaxTriviaList));
        }

        if (separatorCount < switchExpr.Arms.Count)
            nodes.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));

        return switchExpr.WithArms(
            SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(nodes));
    }

    private static SwitchExpressionSyntax AppendExpressionArms(
        SwitchExpressionSyntax switchExpr,
        IReadOnlyList<SwitchExpressionArmSyntax> newArms,
        string lineEnding,
        SyntaxTriviaList indentation)
    {
        if (newArms.Count == 0)
            return switchExpr;

        var nodes = switchExpr.Arms.GetWithSeparators().ToList();
        var hasTrailingComma = nodes.Count > 0 && nodes[nodes.Count - 1].IsToken;
        var trailingArmTrivia = default(SyntaxTriviaList);
        var closeBraceLeading = switchExpr.CloseBraceToken.LeadingTrivia;
        var moveCloseTrivia = HasCommentTrivia(closeBraceLeading);
        if (switchExpr.Arms.Count > 0 && !hasTrailingComma)
        {
            var lastArmIndex = nodes.Count - 1;
            var lastArm = nodes[lastArmIndex].AsNode() as SwitchExpressionArmSyntax;
            if (lastArm is not null)
            {
                nodes[lastArmIndex] = DetachTrailingTrivia(
                    lastArm,
                    out trailingArmTrivia);
            }
        }
        else if (hasTrailingComma)
        {
            var separatorIndex = nodes.Count - 1;
            var separator = nodes[separatorIndex].AsToken();
            trailingArmTrivia = separator.TrailingTrivia;
            nodes[separatorIndex] = separator.WithTrailingTrivia(default(SyntaxTriviaList));
            if (!HasLineBreak(trailingArmTrivia))
            {
                trailingArmTrivia = AppendLineBreak(
                    trailingArmTrivia,
                    lineEnding,
                    indentation);
            }
        }

        if (moveCloseTrivia)
        {
            trailingArmTrivia = ConcatTrivia(
                trailingArmTrivia,
                closeBraceLeading);
        }

        var separatorTrivia = SyntaxFactory.ParseTrailingTrivia(
            lineEnding + indentation.ToFullString());
        for (var index = 0; index < newArms.Count; index++)
        {
            if ((index == 0 && switchExpr.Arms.Count > 0 && !hasTrailingComma) ||
                index > 0)
            {
                var trailingTrivia = index == 0 && trailingArmTrivia.Count > 0
                    ? AppendLineBreak(trailingArmTrivia, lineEnding, indentation)
                    : separatorTrivia;
                nodes.Add(SyntaxFactory.Token(SyntaxKind.CommaToken)
                    .WithTrailingTrivia(trailingTrivia));
            }

            nodes.Add(newArms[index].WithLeadingTrivia(
                index == 0 && hasTrailingComma
                    ? trailingArmTrivia
                    : default(SyntaxTriviaList)));
        }

        var finalComma = SyntaxFactory.Token(SyntaxKind.CommaToken);
        if (!HasLineBreak(closeBraceLeading))
        {
            finalComma = finalComma.WithTrailingTrivia(
                SyntaxFactory.ParseTrailingTrivia(
                    lineEnding + GetTrailingWhitespace(closeBraceLeading)));
        }

        nodes.Add(finalComma);
        var updatedSwitch = switchExpr.WithArms(
            SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(nodes));
        if (moveCloseTrivia)
        {
            updatedSwitch = updatedSwitch.WithCloseBraceToken(
                updatedSwitch.CloseBraceToken.WithLeadingTrivia(
                    SyntaxFactory.ParseLeadingTrivia(
                        lineEnding + GetTrailingWhitespace(closeBraceLeading))));
        }

        return updatedSwitch;
    }

    private static SwitchExpressionArmSyntax DetachTrailingTrivia(
        SwitchExpressionArmSyntax arm,
        out SyntaxTriviaList trailingTrivia)
    {
        var lastToken = arm.GetLastToken();
        trailingTrivia = lastToken.TrailingTrivia;
        return arm.ReplaceToken(
            lastToken,
            lastToken.WithTrailingTrivia(default(SyntaxTriviaList)));
    }

    private static SyntaxTriviaList AppendLineBreak(
        SyntaxTriviaList existing,
        string lineEnding,
        SyntaxTriviaList indentation)
    {
        if (HasLineBreak(existing))
            return existing;

        var values = existing.ToList();
        values.AddRange(
            SyntaxFactory.ParseTrailingTrivia(
                lineEnding + indentation.ToFullString()));
        return SyntaxFactory.TriviaList(values);
    }

    private static SyntaxTriviaList ConcatTrivia(
        SyntaxTriviaList first,
        SyntaxTriviaList second)
    {
        var values = first.ToList();
        values.AddRange(second);
        return SyntaxFactory.TriviaList(values);
    }

    private static bool HasLineBreak(SyntaxTriviaList trivia) =>
        trivia.Any(value => value.IsKind(SyntaxKind.EndOfLineTrivia));

    private static bool HasCommentTrivia(SyntaxTriviaList trivia) =>
        trivia.Any(value => value.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                            value.IsKind(SyntaxKind.MultiLineCommentTrivia));

    private static PatternSyntax? ParsePattern(string text)
    {
        // Parse through a switch expression so this remains compatible with
        // Roslyn versions that do not expose SyntaxFactory.ParsePattern.
        var expression = SyntaxFactory.ParseExpression(
            $"__spire_pattern_subject switch {{ {text} => 0 }}");
        if (expression.ContainsDiagnostics || expression.GetDiagnostics().Any(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error))
            return null;

        if (expression is not SwitchExpressionSyntax switchExpression ||
            switchExpression.Arms.Count != 1)
            return null;

        var pattern = switchExpression.Arms[0].Pattern
            .WithoutTrivia()
            .WithAdditionalAnnotations(Simplifier.Annotation);
        var names = pattern.DescendantNodesAndSelf()
            .Where(node => node is NameSyntax or MemberAccessExpressionSyntax)
            .ToArray();
        return pattern.ReplaceNodes(
            names,
            (original, _) => original.WithAdditionalAnnotations(Simplifier.Annotation));
    }

    private static SwitchSectionSyntax BuildExactSection(PatternSyntax pattern)
    {
        SwitchLabelSyntax label;
        if (pattern is ConstantPatternSyntax constant)
        {
            label = SyntaxFactory.CaseSwitchLabel(
                constant.Expression.WithoutTrivia());
        }
        else
        {
            label = SyntaxFactory.CasePatternSwitchLabel(
                pattern.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.ColonToken));
        }

        return MakeSection(label);
    }

    // ─── Switch expression ───

    private static Document ApplySwitchExpression(
        Document document, SyntaxNode root,
        SwitchExpressionSyntax switchExpr, INamedTypeSymbol subjectType,
        string[] missingVariants,
        SemanticModel semanticModel,
        OptionSet formattingOptions)
    {
        bool isStruct = subjectType.IsValueType;
        var kindPrefix = StructPropertyPatternBuilder.GetKindPrefix(subjectType);
        var leadingTrivia = GetArmLeadingTrivia(switchExpr, formattingOptions);

        var newArms = new List<SwitchExpressionArmSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();
            SwitchExpressionArmSyntax arm;

            if (trimmed == "null")
            {
                arm = SyntaxFactory.SwitchExpressionArm(
                    ParsePattern("null")!,
                    ThrowNotImplementedExpr());
            }
            else if (isStruct)
            {
                var factory = FindStaticFactory(subjectType, trimmed);
                arm = BuildStructPropertyArm(
                    trimmed,
                    kindPrefix,
                    factory,
                    subjectType,
                    switchExpr,
                    semanticModel);
            }
            else
            {
                arm = BuildTypePatternArm(
                    trimmed,
                    subjectType,
                    switchExpr,
                    semanticModel);
            }

            newArms.Add(arm.WithLeadingTrivia(leadingTrivia));
        }

        var updatedSwitch = switchExpr.AddArms(newArms.ToArray());
        var newRoot = root.ReplaceNode(switchExpr, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }

    // ─── Switch statement ───

    private static Document ApplySwitchStatement(
        Document document, SyntaxNode root,
        SwitchStatementSyntax switchStmt, INamedTypeSymbol subjectType,
        string[] missingVariants,
        SemanticModel semanticModel,
        OptionSet formattingOptions)
    {
        bool isStruct = subjectType.IsValueType;
        var kindPrefix = StructPropertyPatternBuilder.GetKindPrefix(subjectType);
        var newSections = new List<SwitchSectionSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();
            SwitchSectionSyntax section;

            if (trimmed == "null")
            {
                section = BuildExactSection(ParsePattern("null")!);
            }
            else if (isStruct)
            {
                var factory = FindStaticFactory(subjectType, trimmed);
                section = BuildStructPropertySection(
                    trimmed,
                    kindPrefix,
                    factory,
                    subjectType,
                    switchStmt,
                    semanticModel);
            }
            else
            {
                section = BuildTypePatternSection(
                    trimmed,
                    subjectType,
                    switchStmt,
                    semanticModel);
            }

            newSections.Add(section);
        }

        var updatedSwitch = AppendSections(switchStmt, newSections, formattingOptions);
        var newRoot = root.ReplaceNode(switchStmt, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }

    private static SwitchStatementSyntax AppendSections(
        SwitchStatementSyntax switchStmt,
        IEnumerable<SwitchSectionSyntax> sections,
        OptionSet formattingOptions)
    {
        var newSections = sections.ToArray();
        if (newSections.Length == 0)
            return switchStmt;

        switchStmt = ExpandEmptyInlineSwitch(switchStmt);

        var sectionLeadingTrivia = GetSectionLineLeadingTrivia(
            switchStmt,
            formattingOptions);
        var statementLeadingTrivia = GetStatementLineLeadingTrivia(
            switchStmt,
            sectionLeadingTrivia,
            formattingOptions);
        var lineEnding = GetLineEnding(sectionLeadingTrivia);
        var sectionIndentation = GetIndentationTrivia(sectionLeadingTrivia);
        var statementIndentation = GetIndentationTrivia(statementLeadingTrivia);
        var originalLastTokenTrailingTrivia = switchStmt.Sections
            .LastOrDefault()
            ?.GetLastToken()
            .TrailingTrivia;
        var originalTrailingTrivia = originalLastTokenTrailingTrivia ??
            switchStmt.OpenBraceToken.TrailingTrivia;
        var updatedSwitch = switchStmt.AddSections(newSections);
        for (var index = 0; index < newSections.Length; index++)
        {
            var sectionIndex = switchStmt.Sections.Count + index;
            var section = updatedSwitch.Sections[sectionIndex];
            var previousToken = section.GetFirstToken().GetPreviousToken();
            var previousTrailingTrivia = index == 0
                ? originalTrailingTrivia
                : previousToken.TrailingTrivia;
            updatedSwitch = updatedSwitch.ReplaceToken(
                previousToken,
                previousToken.WithTrailingTrivia(
                    EnsureLineBreak(previousTrailingTrivia, lineEnding)));

            section = updatedSwitch.Sections[sectionIndex];
            updatedSwitch = updatedSwitch.ReplaceNode(
                section,
                ApplySectionTrivia(
                    section,
                    sectionIndentation,
                    statementIndentation,
                    lineEnding));
        }

        var closeBrace = updatedSwitch.CloseBraceToken;
        var closeBraceLeading = CollapseWhitespaceOnlyLineBreaks(
            closeBrace.LeadingTrivia);
        if (closeBraceLeading.ToFullString() != closeBrace.LeadingTrivia.ToFullString())
        {
            updatedSwitch = updatedSwitch.WithCloseBraceToken(
                closeBrace.WithLeadingTrivia(closeBraceLeading));
        }

        return updatedSwitch;
    }

    private static SwitchStatementSyntax ExpandEmptyInlineSwitch(
        SwitchStatementSyntax switchStmt)
    {
        if (switchStmt.Sections.Count > 0)
            return switchStmt;

        var source = switchStmt.SyntaxTree.GetText();
        var openLine = source.Lines.GetLineFromPosition(switchStmt.OpenBraceToken.SpanStart);
        var closeLine = source.Lines.GetLineFromPosition(switchStmt.CloseBraceToken.SpanStart);
        if (openLine.LineNumber != closeLine.LineNumber)
            return switchStmt;

        // An inline closing brace has no indentation to copy. Anchor the
        // expanded body to the switch's source line instead.
        var switchLine = source.Lines.GetLineFromPosition(switchStmt.SwitchKeyword.SpanStart);
        var indentationEnd = switchLine.Start;
        while (indentationEnd < switchStmt.SwitchKeyword.SpanStart &&
               (source[indentationEnd] == ' ' || source[indentationEnd] == '\t'))
            indentationEnd++;

        var indentation = source.ToString(TextSpan.FromBounds(switchLine.Start, indentationEnd));
        var lineEnding = GetLineEnding(source.ToString());
        return switchStmt
            .WithOpenBraceToken(switchStmt.OpenBraceToken.WithTrailingTrivia(
                EnsureLineBreak(switchStmt.OpenBraceToken.TrailingTrivia, lineEnding)))
            .WithCloseBraceToken(switchStmt.CloseBraceToken.WithLeadingTrivia(
                ConcatTrivia(
                    switchStmt.CloseBraceToken.LeadingTrivia,
                    SyntaxFactory.ParseLeadingTrivia(indentation))));
    }

    // ─── Shared helpers ───

    private static IMethodSymbol? FindStaticFactory(INamedTypeSymbol type, string name) =>
        type.GetMembers(StructPropertyPatternBuilder.NormalizeIdentifier(name))
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic);

    /// Gets positional field info for a record/class DU variant by looking at
    /// its Deconstruct method, then falling back to its primary constructor.
    private static ImmutableArray<IParameterSymbol> GetVariantFields(
        INamedTypeSymbol subjectType, string variantName)
    {
        var variantType = subjectType.GetTypeMembers(variantName).FirstOrDefault();
        if (variantType is null) return ImmutableArray<IParameterSymbol>.Empty;

        var deconstruct = variantType.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.All(p => p.RefKind == RefKind.Out))
            .OrderByDescending(m => m.Parameters.Length)
            .FirstOrDefault();
        if (deconstruct is not null) return deconstruct.Parameters;

        var ctor = variantType.InstanceConstructors
            .Where(c => c.Parameters.Length > 0)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();
        return ctor?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
    }

    private static List<SubpatternSyntax> BuildFieldSubpatterns(
        ImmutableArray<IParameterSymbol> fields)
    {
        var result = new List<SubpatternSyntax>();
        foreach (var param in fields)
        {
            var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            result.Add(SyntaxFactory.Subpattern(
                SyntaxFactory.DeclarationPattern(
                    SyntaxFactory.ParseTypeName(typeName),
                    SyntaxFactory.SingleVariableDesignation(
                        SyntaxFactory.Identifier(param.Name)))));
        }
        return result;
    }

    private static ThrowExpressionSyntax ThrowNotImplementedExpr() =>
        SyntaxFactory.ThrowExpression(
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("System.NotImplementedException"))
            .WithArgumentList(SyntaxFactory.ArgumentList()));

    private static ThrowStatementSyntax ThrowNotImplementedStmt() =>
        SyntaxFactory.ThrowStatement(
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("System.NotImplementedException"))
            .WithArgumentList(SyntaxFactory.ArgumentList()));

    private static SwitchSectionSyntax MakeSection(SwitchLabelSyntax label)
    {
        var section = SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList(label),
            SyntaxFactory.SingletonList<StatementSyntax>(ThrowNotImplementedStmt()));
        return section.NormalizeWhitespace(elasticTrivia: false);
    }

    private static SyntaxTriviaList GetArmLeadingTrivia(
        SwitchExpressionSyntax switchExpr,
        OptionSet formattingOptions)
    {
        var existingArm = switchExpr.Arms.FirstOrDefault();
        return existingArm is not null
            ? GetIndentationTrivia(GetTokenLineLeadingTrivia(existingArm.GetFirstToken()))
            : GetIndentationTrivia(GetEmptyMemberLineLeadingTrivia(
                switchExpr.OpenBraceToken.TrailingTrivia,
                switchExpr.CloseBraceToken.LeadingTrivia,
                formattingOptions));
    }

    private static SyntaxTriviaList GetArmLineLeadingTrivia(
        SwitchExpressionSyntax switchExpr,
        OptionSet formattingOptions)
    {
        var existingArm = switchExpr.Arms.FirstOrDefault();
        return existingArm is not null
            ? GetTokenLineLeadingTrivia(existingArm.GetFirstToken())
            : GetEmptyMemberLineLeadingTrivia(
                switchExpr.OpenBraceToken.TrailingTrivia,
                switchExpr.CloseBraceToken.LeadingTrivia,
                formattingOptions);
    }

    private static SyntaxTriviaList GetSectionLineLeadingTrivia(
        SwitchStatementSyntax switchStmt,
        OptionSet formattingOptions)
    {
        var existingSection = switchStmt.Sections.FirstOrDefault();
        return existingSection is not null
            ? GetTokenLineLeadingTrivia(existingSection.GetFirstToken())
            : GetEmptyMemberLineLeadingTrivia(
                switchStmt.OpenBraceToken.TrailingTrivia,
                switchStmt.CloseBraceToken.LeadingTrivia,
                formattingOptions);
    }

    private static SyntaxTriviaList GetStatementLineLeadingTrivia(
        SwitchStatementSyntax switchStmt,
        SyntaxTriviaList sectionLeadingTrivia,
        OptionSet formattingOptions)
    {
        var sectionIndentation = GetIndentationTrivia(sectionLeadingTrivia);
        var sectionIndentationLength = sectionIndentation.ToFullString().Length;
        var candidates = switchStmt.Sections
            .SelectMany(section => section.Statements)
            .SelectMany(statement => statement
                .DescendantNodesAndSelf()
                .OfType<StatementSyntax>())
            .Select(statement => GetTokenLineLeadingTrivia(statement.GetFirstToken()))
            .Where(leading =>
                leading.Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
            .Select(GetIndentationTrivia)
            .Where(indentation =>
                indentation.ToFullString().Length > sectionIndentationLength)
            .OrderBy(indentation => indentation.ToFullString().Length)
            .FirstOrDefault();
        if (candidates.Count > 0)
            return candidates;

        return SyntaxFactory.ParseLeadingTrivia(
            GetLineEnding(sectionLeadingTrivia) +
            sectionIndentation.ToFullString() +
            GetIndentationUnit(formattingOptions));
    }

    private static SwitchSectionSyntax ApplySectionTrivia(
        SwitchSectionSyntax section,
        SyntaxTriviaList sectionLeadingTrivia,
        SyntaxTriviaList statementLeadingTrivia,
        string lineEnding)
    {
        var label = section.Labels[0].WithLeadingTrivia(sectionLeadingTrivia);
        var labelLastToken = label.GetLastToken();
        label = label.ReplaceToken(
            labelLastToken,
            labelLastToken.WithTrailingTrivia(
                SyntaxFactory.ParseTrailingTrivia(lineEnding)));
        var statements = section.Statements
            .Select(statement =>
            {
                var adjusted = statement.WithLeadingTrivia(statementLeadingTrivia);
                var lastToken = adjusted.GetLastToken();
                return adjusted.ReplaceToken(
                    lastToken,
                    lastToken.WithTrailingTrivia(
                        SyntaxFactory.ParseTrailingTrivia(lineEnding)));
            });
        return section
            .WithLabels(SyntaxFactory.SingletonList(label))
            .WithStatements(SyntaxFactory.List(statements));
    }

    private static SyntaxTriviaList GetTokenLineLeadingTrivia(SyntaxToken token)
    {
        var text = token.GetPreviousToken().TrailingTrivia.ToFullString() +
            token.LeadingTrivia.ToFullString();
        return GetFullLineLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(text));
    }

    private static SyntaxTriviaList GetEmptyMemberLineLeadingTrivia(
        SyntaxTriviaList openBraceTrailingTrivia,
        SyntaxTriviaList closeBraceLeadingTrivia,
        OptionSet formattingOptions)
    {
        var closeIndent = GetTrailingWhitespace(closeBraceLeadingTrivia);
        var unit = GetIndentationUnit(formattingOptions);
        return SyntaxFactory.ParseLeadingTrivia(
            GetLineEnding(openBraceTrailingTrivia) + closeIndent + unit);
    }

    private static string GetIndentationUnit(OptionSet formattingOptions)
    {
        var useTabs = formattingOptions.GetOption(
            FormattingOptions.UseTabs,
            LanguageNames.CSharp);
        var size = formattingOptions.GetOption(
            FormattingOptions.IndentationSize,
            LanguageNames.CSharp);
        if (useTabs)
        {
            var tabSize = formattingOptions.GetOption(
                FormattingOptions.TabSize,
                LanguageNames.CSharp);
            if (tabSize > 0)
                return new string('\t', size / tabSize) +
                    new string(' ', size % tabSize);
        }

        return new string(' ', size);
    }

    private static string GetLineEnding(SyntaxTriviaList trivia)
    {
        return GetLineEnding(trivia.ToFullString());
    }

    private static string GetLineEnding(string text)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (text[index] == '\n')
                return index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
            if (text[index] == '\r')
                return "\r";
        }

        return "\n";
    }

    private static SyntaxTriviaList EnsureLineBreak(
        SyntaxTriviaList trivia,
        string lineEnding)
    {
        var result = new List<SyntaxTrivia>();
        var trimIndentation = false;
        var hasLineBreak = false;
        foreach (var value in trivia)
        {
            if (value.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                result.Add(value);
                trimIndentation = true;
                hasLineBreak = true;
                continue;
            }

            if (trimIndentation && value.IsKind(SyntaxKind.WhitespaceTrivia))
                continue;

            result.Add(value);
            trimIndentation = false;
        }

        if (!hasLineBreak)
            result.Add(SyntaxFactory.EndOfLine(lineEnding));

        return SyntaxFactory.TriviaList(result);
    }

    private static SyntaxTriviaList CollapseWhitespaceOnlyLineBreaks(
        SyntaxTriviaList trivia)
    {
        if (!trivia.Any(value => value.IsKind(SyntaxKind.EndOfLineTrivia)) ||
            trivia.Any(value =>
                !value.IsKind(SyntaxKind.EndOfLineTrivia) &&
                !value.IsKind(SyntaxKind.WhitespaceTrivia)))
        {
            return trivia;
        }

        return SyntaxFactory.ParseLeadingTrivia(GetTrailingWhitespace(trivia));
    }

    private static SyntaxTriviaList GetIndentationTrivia(
        SyntaxTriviaList lineLeadingTrivia)
    {
        var text = lineLeadingTrivia.ToFullString();
        return SyntaxFactory.ParseLeadingTrivia(GetTrailingWhitespace(text));
    }

    private static SyntaxTriviaList GetFullLineLeadingTrivia(
        SyntaxTriviaList trivia)
    {
        var text = trivia.ToFullString();
        var lineBreak = -1;
        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (text[index] is '\r' or '\n')
            {
                lineBreak = index;
                break;
            }
        }

        if (lineBreak < 0)
            return SyntaxFactory.ParseLeadingTrivia(text);

        var indentationStart = lineBreak + 1;
        while (indentationStart < text.Length &&
               (text[indentationStart] == ' ' || text[indentationStart] == '\t'))
        {
            indentationStart++;
        }

        var lineStart = text[lineBreak] == '\n' && lineBreak > 0 && text[lineBreak - 1] == '\r'
            ? lineBreak - 1
            : lineBreak;
        return SyntaxFactory.ParseLeadingTrivia(
            text.Substring(lineStart, indentationStart - lineStart));
    }

    private static string GetTrailingWhitespace(SyntaxTriviaList trivia)
    {
        return GetTrailingWhitespace(trivia.ToFullString());
    }

    private static string GetTrailingWhitespace(string text)
    {
        var index = text.Length - 1;
        while (index >= 0 && (text[index] == ' ' || text[index] == '\t'))
            index--;
        return text.Substring(index + 1);
    }

    // ─── Struct DU: switch expression arms ───

    private static SwitchExpressionArmSyntax BuildStructPropertyArm(
        string variantName,
        string kindPrefix,
        IMethodSymbol? factory,
        INamedTypeSymbol subjectType,
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var pattern = StructPropertyPatternBuilder.Build(
            subjectType,
            variantName,
            factory,
            scope,
            kindPrefix,
            semanticModel,
            useVariantPredicate: true);

        return SyntaxFactory.SwitchExpressionArm(pattern, ThrowNotImplementedExpr());
    }

    // ─── Record/class DU: switch expression arms ───

    private static string GetVariantTypeName(
        string variantName,
        INamedTypeSymbol subjectType,
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var subjectName = subjectType.ToMinimalDisplayString(
            semanticModel,
            scope.SpanStart);
        return $"{subjectName}.{variantName}";
    }

    private static SwitchExpressionArmSyntax BuildTypePatternArm(
        string variantName,
        INamedTypeSymbol subjectType,
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var fields = GetVariantFields(subjectType, variantName);
        var qualifiedName = GetVariantTypeName(
            variantName,
            subjectType,
            scope,
            semanticModel);

        PatternSyntax pattern;
        if (fields.IsEmpty)
        {
            pattern = SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression(qualifiedName));
        }
        else
        {
            var subpatterns = BuildFieldSubpatterns(fields);
            pattern = SyntaxFactory.RecursivePattern()
                .WithType(SyntaxFactory.ParseTypeName(qualifiedName))
                .WithPositionalPatternClause(
                    SyntaxFactory.PositionalPatternClause(
                        SyntaxFactory.SeparatedList(subpatterns)));
        }

        return SyntaxFactory.SwitchExpressionArm(pattern, ThrowNotImplementedExpr());
    }

    // ─── Struct DU: switch statement sections ───

    private static SwitchSectionSyntax BuildStructPropertySection(
        string variantName,
        string kindPrefix,
        IMethodSymbol? factory,
        INamedTypeSymbol subjectType,
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var pattern = StructPropertyPatternBuilder.Build(
            subjectType,
            variantName,
            factory,
            scope,
            kindPrefix,
            semanticModel,
            useVariantPredicate: true);

        // A newly inserted switch section has no surrounding formatter trivia.
        // Keep the opening brace free of line trivia; the case keyword below
        // supplies the required inline separator.
        if (pattern.PropertyPatternClause is { } existingClause)
        {
            var propertyClause = existingClause.WithOpenBraceToken(
                existingClause.OpenBraceToken.WithLeadingTrivia(default(SyntaxTriviaList)));
            pattern = pattern.WithPropertyPatternClause(propertyClause);
        }

        var label = SyntaxFactory.CasePatternSwitchLabel(
            pattern, SyntaxFactory.Token(SyntaxKind.ColonToken));
        label = label.WithKeyword(
            label.Keyword.WithTrailingTrivia(
                SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(" "))));
        return MakeSection(label);
    }

    // ─── Record/class DU: switch statement sections ───

    private static SwitchSectionSyntax BuildTypePatternSection(
        string variantName,
        INamedTypeSymbol subjectType,
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var fields = GetVariantFields(subjectType, variantName);
        var qualifiedName = GetVariantTypeName(
            variantName,
            subjectType,
            scope,
            semanticModel);

        if (fields.IsEmpty)
        {
            // case State.MainMenu:
            return MakeSection(SyntaxFactory.CaseSwitchLabel(
                SyntaxFactory.ParseExpression(qualifiedName)));
        }

        // case State.RunningJourney(Type1 p1, Type2 p2):
        var subpatterns = BuildFieldSubpatterns(fields);
        var pattern = SyntaxFactory.RecursivePattern()
            .WithType(SyntaxFactory.ParseTypeName(qualifiedName))
            .WithPositionalPatternClause(
                SyntaxFactory.PositionalPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

        var section = MakeSection(
            SyntaxFactory.CasePatternSwitchLabel(
                pattern,
                SyntaxFactory.Token(SyntaxKind.ColonToken)));
        var label = (CasePatternSwitchLabelSyntax)section.Labels[0];
        if (label.Pattern is RecursivePatternSyntax recursivePattern &&
            recursivePattern.Type is { } recursiveType &&
            recursivePattern.PositionalPatternClause is { } positionalClause)
        {
            var typeLastToken = recursiveType.GetLastToken();
            recursivePattern = recursivePattern.ReplaceToken(
                typeLastToken,
                typeLastToken.WithTrailingTrivia(default(SyntaxTriviaList)));
            recursivePattern = recursivePattern.WithPositionalPatternClause(
                positionalClause.WithOpenParenToken(
                    positionalClause.OpenParenToken.WithLeadingTrivia(default(SyntaxTriviaList))));
            section = section.WithLabels(
                SyntaxFactory.SingletonList<SwitchLabelSyntax>(
                    label.WithPattern(recursivePattern)));
        }
        return section;
    }
}
