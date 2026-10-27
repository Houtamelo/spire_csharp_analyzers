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

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class AddMissingArmsCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE009");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var missingStr = diagnostic.Properties.GetValueOrDefault("MissingVariants");
        if (missingStr is null or { Length: 0 }) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add missing variant arms",
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

        var missingVariantsStr = diagnostic.Properties["MissingVariants"];
        if (missingVariantsStr is null) return document;
        var missingVariants = missingVariantsStr.Split(',');

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // Try switch expression
        var switchExpr = node.AncestorsAndSelf()
            .OfType<SwitchExpressionSyntax>()
            .FirstOrDefault();
        if (switchExpr is not null)
        {
            var subjectType = model.GetTypeInfo(switchExpr.GoverningExpression, ct).Type
                as INamedTypeSymbol;
            if (subjectType is null) return document;

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
        var switchStmt = node.AncestorsAndSelf()
            .OfType<SwitchStatementSyntax>()
            .FirstOrDefault();
        if (switchStmt is not null)
        {
            var subjectType = model.GetTypeInfo(switchStmt.Expression, ct).Type
                as INamedTypeSymbol;
            if (subjectType is null) return document;

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

            if (isStruct)
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
                arm = BuildTypePatternArm(trimmed, subjectType);
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
        var sectionLeadingTrivia = GetSectionLineLeadingTrivia(switchStmt, formattingOptions);
        var statementLeadingTrivia = GetStatementLineLeadingTrivia(
            switchStmt,
            sectionLeadingTrivia,
            formattingOptions);
        var lineEnding = GetLineEnding(sectionLeadingTrivia);

        var newSections = new List<SwitchSectionSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();
            SwitchSectionSyntax section;

            if (isStruct)
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
                section = BuildTypePatternSection(trimmed, subjectType);
            }

            newSections.Add(section);
        }

        var sectionIndentation = GetIndentationTrivia(sectionLeadingTrivia);
        var statementIndentation = GetIndentationTrivia(statementLeadingTrivia);
        var originalLastTokenTrailingTrivia = switchStmt.Sections
            .LastOrDefault()
            ?.GetLastToken()
            .TrailingTrivia;
        var originalTrailingTrivia = originalLastTokenTrailingTrivia.GetValueOrDefault();
        var updatedSwitch = switchStmt.AddSections(newSections.ToArray());
        for (var index = 0; index < newSections.Count; index++)
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
        var newRoot = root.ReplaceNode(switchStmt, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
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
        var existingStatement = switchStmt.Sections
            .SelectMany(section => section.Statements)
            .FirstOrDefault();
        if (existingStatement is not null)
            return GetTokenLineLeadingTrivia(existingStatement.GetFirstToken());

        return SyntaxFactory.ParseLeadingTrivia(
            GetLineEnding(sectionLeadingTrivia) +
            GetTrailingWhitespace(sectionLeadingTrivia) +
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
        var text = trivia.ToFullString();
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

    private static SwitchExpressionArmSyntax BuildTypePatternArm(
        string variantName, INamedTypeSymbol subjectType)
    {
        var fields = GetVariantFields(subjectType, variantName);
        var qualifiedName = $"{subjectType.Name}.{variantName}";

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
        string variantName, INamedTypeSymbol subjectType)
    {
        var fields = GetVariantFields(subjectType, variantName);
        var qualifiedName = $"{subjectType.Name}.{variantName}";

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
