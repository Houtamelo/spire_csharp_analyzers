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

            return ApplySwitchExpression(document, root, switchExpr, subjectType, missingVariants);
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

            return ApplySwitchStatement(document, root, switchStmt, subjectType, missingVariants);
        }

        return document;
    }

    // ─── Switch expression ───

    private static Document ApplySwitchExpression(
        Document document, SyntaxNode root,
        SwitchExpressionSyntax switchExpr, INamedTypeSymbol subjectType,
        string[] missingVariants)
    {
        bool isStruct = subjectType.IsValueType;
        bool usePropertyPattern = isStruct && ExistingArmsUsePropertyPattern(switchExpr);
        var kindPrefix = $"{subjectType.Name}.Kind";

        var newArms = new List<SwitchExpressionArmSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();
            SwitchExpressionArmSyntax arm;

            if (isStruct)
            {
                var factory = FindStaticFactory(subjectType, trimmed);
                arm = usePropertyPattern
                    ? BuildStructPropertyArm(trimmed, kindPrefix, factory)
                    : BuildStructPositionalArm(trimmed, kindPrefix, factory, subjectType);
            }
            else
            {
                arm = BuildTypePatternArm(trimmed, subjectType);
            }

            newArms.Add(arm);
        }

        var updatedSwitch = switchExpr.AddArms(newArms.ToArray());
        var newRoot = root.ReplaceNode(switchExpr, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }

    // ─── Switch statement ───

    private static Document ApplySwitchStatement(
        Document document, SyntaxNode root,
        SwitchStatementSyntax switchStmt, INamedTypeSymbol subjectType,
        string[] missingVariants)
    {
        bool isStruct = subjectType.IsValueType;
        bool usePropertyPattern = isStruct && ExistingStmtLabelsUsePropertyPattern(switchStmt);
        var kindPrefix = $"{subjectType.Name}.Kind";

        var newSections = new List<SwitchSectionSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();
            SwitchSectionSyntax section;

            if (isStruct)
            {
                var factory = FindStaticFactory(subjectType, trimmed);
                section = usePropertyPattern
                    ? BuildStructPropertySection(trimmed, kindPrefix, factory)
                    : BuildStructPositionalSection(trimmed, kindPrefix, factory, subjectType);
            }
            else
            {
                section = BuildTypePatternSection(trimmed, subjectType);
            }

            newSections.Add(section);
        }

        var updatedSwitch = switchStmt.AddSections(newSections.ToArray());
        var newRoot = root.ReplaceNode(switchStmt, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }

    // ─── Pattern detection ───

    private static bool ExistingArmsUsePropertyPattern(SwitchExpressionSyntax switchExpr)
    {
        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is RecursivePatternSyntax recursive &&
                recursive.PropertyPatternClause is not null)
                return true;
        }
        return false;
    }

    private static bool ExistingStmtLabelsUsePropertyPattern(SwitchStatementSyntax switchStmt)
    {
        foreach (var section in switchStmt.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is CasePatternSwitchLabelSyntax { Pattern: RecursivePatternSyntax recursive } &&
                    recursive.PropertyPatternClause is not null)
                    return true;
            }
        }
        return false;
    }

    // ─── Shared helpers ───

    private static IMethodSymbol? FindStaticFactory(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(m => m.IsStatic);

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

    private static SwitchSectionSyntax MakeSection(SwitchLabelSyntax label) =>
        SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList(label),
            SyntaxFactory.SingletonList<StatementSyntax>(ThrowNotImplementedStmt()));

    // ─── Struct DU: switch expression arms ───

    private static SwitchExpressionArmSyntax BuildStructPropertyArm(
        string variantName, string kindPrefix, IMethodSymbol? factory)
    {
        var subpatterns = new List<SubpatternSyntax>();

        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("kind")),
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

        if (factory is not null)
        {
            foreach (var param in factory.Parameters)
            {
                var typeName = param.Type.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat);
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(param.Name)),
                    SyntaxFactory.DeclarationPattern(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(param.Name)))));
            }
        }

        var pattern = SyntaxFactory.RecursivePattern()
            .WithPropertyPatternClause(
                SyntaxFactory.PropertyPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

        return SyntaxFactory.SwitchExpressionArm(pattern, ThrowNotImplementedExpr());
    }

    private static SwitchExpressionArmSyntax BuildStructPositionalArm(
        string variantName, string kindPrefix, IMethodSymbol? factory,
        INamedTypeSymbol subjectType)
    {
        int fieldCount = factory?.Parameters.Length ?? 0;
        var subpatterns = new List<SubpatternSyntax>();

        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

        if (factory is not null)
        {
            foreach (var param in factory.Parameters)
            {
                var typeName = param.Type.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat);
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.DeclarationPattern(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(param.Name)))));
            }
        }

        if (fieldCount == 0)
        {
            var hasSharedDeconstruct = subjectType.GetMembers("Deconstruct")
                .OfType<IMethodSymbol>()
                .Any(m => m.Parameters.Length == 2);
            if (hasSharedDeconstruct)
                subpatterns.Add(SyntaxFactory.Subpattern(SyntaxFactory.DiscardPattern()));
        }

        var pattern = SyntaxFactory.RecursivePattern()
            .WithPositionalPatternClause(
                SyntaxFactory.PositionalPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

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
        string variantName, string kindPrefix, IMethodSymbol? factory)
    {
        var subpatterns = new List<SubpatternSyntax>();

        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("kind")),
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

        if (factory is not null)
        {
            foreach (var param in factory.Parameters)
            {
                var typeName = param.Type.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat);
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(param.Name)),
                    SyntaxFactory.DeclarationPattern(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(param.Name)))));
            }
        }

        var pattern = SyntaxFactory.RecursivePattern()
            .WithPropertyPatternClause(
                SyntaxFactory.PropertyPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

        return MakeSection(SyntaxFactory.CasePatternSwitchLabel(
            pattern, SyntaxFactory.Token(SyntaxKind.ColonToken)));
    }

    private static SwitchSectionSyntax BuildStructPositionalSection(
        string variantName, string kindPrefix, IMethodSymbol? factory,
        INamedTypeSymbol subjectType)
    {
        int fieldCount = factory?.Parameters.Length ?? 0;
        var subpatterns = new List<SubpatternSyntax>();

        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

        if (factory is not null)
        {
            foreach (var param in factory.Parameters)
            {
                var typeName = param.Type.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat);
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.DeclarationPattern(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(param.Name)))));
            }
        }

        if (fieldCount == 0)
        {
            var hasSharedDeconstruct = subjectType.GetMembers("Deconstruct")
                .OfType<IMethodSymbol>()
                .Any(m => m.Parameters.Length == 2);
            if (hasSharedDeconstruct)
                subpatterns.Add(SyntaxFactory.Subpattern(SyntaxFactory.DiscardPattern()));
        }

        var pattern = SyntaxFactory.RecursivePattern()
            .WithPositionalPatternClause(
                SyntaxFactory.PositionalPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

        return MakeSection(SyntaxFactory.CasePatternSwitchLabel(
            pattern, SyntaxFactory.Token(SyntaxKind.ColonToken)));
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

        return MakeSection(SyntaxFactory.CasePatternSwitchLabel(
            pattern, SyntaxFactory.Token(SyntaxKind.ColonToken)));
    }
}
