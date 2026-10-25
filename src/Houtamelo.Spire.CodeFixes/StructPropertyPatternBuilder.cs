using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

/// Builds the property patterns emitted for newly generated struct-union arms.
internal static class StructPropertyPatternBuilder
{
    private static readonly SymbolDisplayFormat KindTypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string GetKindPrefix(INamedTypeSymbol subjectType)
    {
        return subjectType.ToDisplayString(KindTypeDisplayFormat) + ".Kind";
    }

    public static string NormalizeIdentifier(string name)
    {
        return name.StartsWith("@", StringComparison.Ordinal)
            ? name.Substring(1)
            : name;
    }

    public static RecursivePatternSyntax Build(
        INamedTypeSymbol subjectType,
        string variantName,
        IMethodSymbol? factory,
        SyntaxNode scope,
        string kindPrefix,
        SemanticModel semanticModel)
    {
        variantName = NormalizeIdentifier(variantName);

        var subpatterns = new List<SubpatternSyntax>
        {
            SyntaxFactory.Subpattern(
                SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("kind")),
                SyntaxFactory.ConstantPattern(BuildKindExpression(kindPrefix, variantName)))
        };

        if (factory is not null)
        {
            var usedNames = CollectEnclosingNames(scope, semanticModel);
            var accessorName = ResolveAccessorName(subjectType, variantName);
            var fieldSubpatterns = new List<SubpatternSyntax>();
            foreach (var parameter in factory.Parameters)
            {
                var captureName = MakeUniqueName(parameter.Name, usedNames);
                usedNames.Add(captureName);

                fieldSubpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.NameColon(
                        SyntaxFactory.IdentifierName(EscapeIdentifier(parameter.Name))),
                    SyntaxFactory.VarPattern(
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(EscapeIdentifier(captureName))))));
            }

            if (fieldSubpatterns.Count > 0)
            {
                var payloadPattern = SyntaxFactory.RecursivePattern()
                    .WithPropertyPatternClause(
                        SyntaxFactory.PropertyPatternClause(
                            SyntaxFactory.SeparatedList(fieldSubpatterns)));
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.NameColon(
                        SyntaxFactory.IdentifierName(EscapeIdentifier(accessorName))),
                    payloadPattern));
            }
        }

        return SyntaxFactory.RecursivePattern()
            .WithPropertyPatternClause(
                SyntaxFactory.PropertyPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)))
            .NormalizeWhitespace();
    }

    private static ExpressionSyntax BuildKindExpression(
        string kindPrefix,
        string variantName)
    {
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseExpression(kindPrefix),
            SyntaxFactory.IdentifierName(EscapeIdentifier(variantName)));
    }

    private static string ResolveAccessorName(
        INamedTypeSymbol subjectType,
        string variantName)
    {
        var accessorName = ToCamelCase(variantName);
        var hasFactoryConflict = subjectType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic && IsVariantFactory(method))
            .Any(method => string.Equals(
                NormalizeIdentifier(method.Name),
                accessorName,
                StringComparison.Ordinal));
        return hasFactoryConflict ? accessorName + "_value" : accessorName;
    }

    private static bool IsVariantFactory(IMethodSymbol method)
    {
        return method.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "VariantAttribute");
    }

    private static HashSet<string> CollectEnclosingNames(
        SyntaxNode scope,
        SemanticModel semanticModel)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var symbol in semanticModel.LookupSymbols(scope.SpanStart))
        {
            if (symbol.Kind is SymbolKind.Local or
                SymbolKind.Parameter or
                SymbolKind.RangeVariable)
            {
                names.Add(symbol.Name);
            }
        }

        foreach (var block in scope.Ancestors().OfType<BlockSyntax>())
        {
            foreach (var localDeclaration in block.Statements
                         .OfType<LocalDeclarationStatementSyntax>())
            {
                AddVariableDesignations(localDeclaration.Declaration, names);
            }

            foreach (var variable in block.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>())
            {
                var nearestBlock = variable.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
                if (nearestBlock == block)
                    names.Add(variable.Identifier.ValueText);
            }
        }

        foreach (var ancestor in scope.Ancestors())
        {
            switch (ancestor)
            {
                case IfStatementSyntax statement:
                    AddVariableDesignations(statement.Condition, names);
                    break;
                case WhileStatementSyntax statement:
                    AddVariableDesignations(statement.Condition, names);
                    break;
                case DoStatementSyntax statement:
                    AddVariableDesignations(statement.Condition, names);
                    break;
                case ForStatementSyntax statement:
                    if (statement.Declaration is not null)
                        AddVariableDesignations(statement.Declaration, names);
                    if (statement.Condition is not null)
                        AddVariableDesignations(statement.Condition, names);
                    break;
                case ForEachStatementSyntax statement:
                    names.Add(statement.Identifier.ValueText);
                    AddVariableDesignations(statement.Expression, names);
                    break;
                case ForEachVariableStatementSyntax statement:
                    AddVariableDesignations(statement.Variable, names);
                    AddVariableDesignations(statement.Expression, names);
                    break;
                case UsingStatementSyntax statement:
                    if (statement.Declaration is not null)
                        AddVariableDesignations(statement.Declaration, names);
                    if (statement.Expression is not null)
                        AddVariableDesignations(statement.Expression, names);
                    break;
                case FixedStatementSyntax statement:
                    AddVariableDesignations(statement.Declaration, names);
                    break;
                case SwitchExpressionArmSyntax arm:
                    AddVariableDesignations(arm.Pattern, names);
                    break;
                case CasePatternSwitchLabelSyntax label:
                    AddVariableDesignations(label.Pattern, names);
                    break;
                case CatchFilterClauseSyntax filter:
                    AddVariableDesignations(filter.FilterExpression, names);
                    break;
            }
        }

        foreach (var method in scope.Ancestors().OfType<BaseMethodDeclarationSyntax>())
        {
            foreach (var parameter in method.ParameterList.Parameters)
                names.Add(parameter.Identifier.ValueText);
        }

        foreach (var localFunction in scope.Ancestors().OfType<LocalFunctionStatementSyntax>())
        {
            foreach (var parameter in localFunction.ParameterList.Parameters)
                names.Add(parameter.Identifier.ValueText);
        }

        foreach (var lambda in scope.Ancestors().OfType<ParenthesizedLambdaExpressionSyntax>())
        {
            foreach (var parameter in lambda.ParameterList.Parameters)
                names.Add(parameter.Identifier.ValueText);
        }

        foreach (var lambda in scope.Ancestors().OfType<SimpleLambdaExpressionSyntax>())
            names.Add(lambda.Parameter.Identifier.ValueText);

        foreach (var anonymousMethod in scope.Ancestors().OfType<AnonymousMethodExpressionSyntax>())
        {
            if (anonymousMethod.ParameterList is null)
                continue;

            foreach (var parameter in anonymousMethod.ParameterList.Parameters)
                names.Add(parameter.Identifier.ValueText);
        }

        foreach (var statement in scope.Ancestors().OfType<ForEachStatementSyntax>())
            names.Add(statement.Identifier.ValueText);

        foreach (var declaration in scope.Ancestors().OfType<CatchDeclarationSyntax>())
        {
            if (declaration.Identifier != default)
                names.Add(declaration.Identifier.ValueText);
        }

        return names;
    }

    private static void AddVariableDesignations(
        SyntaxNode node,
        HashSet<string> names)
    {
        foreach (var designation in node
                     .DescendantNodesAndSelf()
                     .OfType<SingleVariableDesignationSyntax>())
        {
            names.Add(designation.Identifier.ValueText);
        }
    }

    private static string MakeUniqueName(string name, HashSet<string> usedNames)
    {
        if (name != "_" && !usedNames.Contains(name))
            return name;

        for (var suffix = 1; ; suffix++)
        {
            var candidate = name + suffix;
            if (!usedNames.Contains(candidate))
                return candidate;
        }
    }

    private static string EscapeIdentifier(string name)
    {
        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None)
            return "@" + name;

        return name;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var uppercasePrefixLength = 0;
        while (uppercasePrefixLength < name.Length &&
               char.IsUpper(name[uppercasePrefixLength]))
        {
            uppercasePrefixLength++;
        }

        if (uppercasePrefixLength == 0)
            return name;

        var lowercaseLength = uppercasePrefixLength;
        if (uppercasePrefixLength > 1 &&
            uppercasePrefixLength < name.Length &&
            char.IsLower(name[uppercasePrefixLength]))
        {
            lowercaseLength--;
        }

        var prefix = name.Substring(0, lowercaseLength).ToLowerInvariant();
        return prefix + name.Substring(lowercaseLength);
    }
}
