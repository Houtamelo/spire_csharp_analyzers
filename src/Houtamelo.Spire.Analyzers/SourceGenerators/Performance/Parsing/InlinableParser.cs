using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinableParser
{
    private const string InlinableAttrFullName = "Houtamelo.Spire.InlinableAttribute";
    private const int MaxDelegateArity = 8;

    public static InlinableHostDecl? Parse(
        IMethodSymbol method,
        MethodDeclarationSyntax mds,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var compilation = semanticModel.Compilation;
        var inlinableAttr = compilation.GetTypeByMetadataName(InlinableAttrFullName);
        if (inlinableAttr is null)
            return null;

        var declaringType = method.ContainingType;
        if (declaringType is null)
            return null;

        // Collect inlinable params.
        var inlinableParams = ImmutableArray.CreateBuilder<InlinableParam>();
        var otherParams = ImmutableArray.CreateBuilder<HostParamInfo>();
        var usedGenericNames = new HashSet<string>(method.TypeParameters.Select(tp => tp.Name));
        usedGenericNames.UnionWith(declaringType.TypeParameters.Select(tp => tp.Name));

        bool isExtensionMethod = method.IsExtensionMethod;
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var p = method.Parameters[i];
            var isInlinable = p.GetAttributes().Any(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, inlinableAttr));
            bool isThis = isExtensionMethod && i == 0;

            if (isInlinable)
            {
                if (!TryBuildInlinable(p, i, usedGenericNames, out var ip))
                {
                    otherParams.Add(BuildHostParamInfo(
                        p,
                        i,
                        isThis,
                        mds.ParameterList.Parameters[i].Type?.ToString()));
                    continue;
                }
                inlinableParams.Add(ip!);
                usedGenericNames.Add(ip!.GenericName);
            }
            else
            {
                otherParams.Add(BuildHostParamInfo(p, i, isThis));
            }
        }

        if (inlinableParams.Count == 0)
            return null;

        var ns = declaringType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : declaringType.ContainingNamespace.ToDisplayString();

        var containingTypes = InlinerSymbolInfo.BuildContainingTypes(declaringType);

        var declaringTypeParameters = new EquatableArray<string>(
            declaringType.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray());
        var declaringTypeParameterConstraints =
            InlinerSymbolInfo.BuildTypeParameterConstraints(declaringType);

        var returnType = method.ReturnsVoid
            ? "void"
            : DisplayType(method.ReturnType);

        var hostTypeParameters = new EquatableArray<string>(
            method.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray());
        var hostConstraints = InlinerSymbolInfo.BuildMethodTypeParameterConstraints(method);

        // Body text: block body or expression body.
        string bodyText;
        if (mds.Body is not null)
            bodyText = mds.Body.ToFullString();
        else if (mds.ExpressionBody is not null)
            bodyText = mds.ExpressionBody.ToFullString();
        else
            bodyText = "";

        var diagnostic = ValidateHost(method, declaringType, mds, inlinableParams);

        // Capture using directives from the original file so the body sees the same
        // namespaces. Includes compilation-unit usings and any namespace-scoped ones
        // the method's containing namespace pulls in.
        var usings = new List<string>();
        var root = mds.SyntaxTree.GetRoot(ct);
        if (root is CompilationUnitSyntax cu)
        {
            foreach (var u in cu.Usings)
                usings.Add(u.ToString().Trim());
        }
        // Walk up namespace declarations to pick up namespace-scoped usings.
        SyntaxNode? walker = mds.Parent;
        while (walker is not null)
        {
            if (walker is BaseNamespaceDeclarationSyntax nsd)
            {
                foreach (var u in nsd.Usings)
                    usings.Add(u.ToString().Trim());
            }
            walker = walker.Parent;
        }

        return new InlinableHostDecl(
            Namespace: ns,
            MethodName: method.Name,
            Accessibility: InlinerSymbolInfo.AccessibilityToKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            ReturnType: returnType,
            DeclaringTypeName: declaringType.Name,
            DeclaringTypeKeyword: InlinerSymbolInfo.GetTypeKeyword(declaringType),
            DeclaringTypeAccessibility: InlinerSymbolInfo.AccessibilityToKeyword(declaringType.DeclaredAccessibility),
            DeclaringTypeParameters: declaringTypeParameters,
            DeclaringTypeParameterConstraints: declaringTypeParameterConstraints,
            ContainingTypes: new EquatableArray<InlinerContainingType>(containingTypes),
            HostTypeParameters: hostTypeParameters,
            HostTypeParameterConstraints: hostConstraints,
            InlinableParams: new EquatableArray<InlinableParam>(inlinableParams.ToImmutable()),
            OtherParams: new EquatableArray<HostParamInfo>(otherParams.ToImmutable()),
            OriginalBody: bodyText,
            UsingDirectives: new EquatableArray<string>(usings.ToImmutableArray()),
            HintName: InlinerHintNaming.ForInlinableTwin(method),
            HasExperimentalAttribute: CompilationInfo.HasAccessibleType(
                compilation, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute"),
            Diagnostic: diagnostic);
    }

    private static string DisplayType(ITypeSymbol type)
        => type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    private static bool TryBuildInlinable(
        IParameterSymbol p,
        int position,
        HashSet<string> usedGenericNames,
        out InlinableParam? result)
    {
        result = null;
        var type = p.Type;

        if (p.RefKind != RefKind.None ||
            RoslynCompatibility.IsRefReadOnlyParameter(p.RefKind) ||
            p.IsParams)
        {
            return false;
        }

        // Unwrap Nullable<T> (only for reference types it's NullableAnnotation; for
        // delegate types which are reference types the annotation is what we check).
        var isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;

        if (type is not INamedTypeSymbol named)
            return false;

        var originalDef = named.OriginalDefinition.ToDisplayString();
        bool isAction = originalDef == "System.Action"
            || originalDef.StartsWith("System.Action<");
        bool isFunc = originalDef.StartsWith("System.Func<");

        if (!isAction && !isFunc)
            return false;

        var typeArgs = named.TypeArguments
            .Select(DisplayType)
            .ToImmutableArray();

        string returnType = "";
        ImmutableArray<string> delegateInputs;
        if (isFunc)
        {
            // Func<T1..Tn, TR>: last type arg is return.
            if (typeArgs.Length == 0)
            {
                delegateInputs = ImmutableArray<string>.Empty;
            }
            else
            {
                returnType = typeArgs[typeArgs.Length - 1];
                delegateInputs = typeArgs.RemoveAt(typeArgs.Length - 1);
            }
        }
        else
        {
            delegateInputs = typeArgs;
        }

        if (delegateInputs.Length > MaxDelegateArity)
            return false;

        var genericName = BuildUniqueGenericName(p.Name, usedGenericNames);

        result = new InlinableParam(
            Name: p.Name,
            Position: position,
            IsNullable: isNullable,
            IsFunc: isFunc,
            DelegateTypeArguments: new EquatableArray<string>(delegateInputs),
            ReturnType: returnType,
            GenericName: genericName);
        return true;
    }

    private static InlinerDiagnostic? ValidateHost(
        IMethodSymbol method,
        INamedTypeSymbol declaringType,
        MethodDeclarationSyntax mds,
        ImmutableArray<InlinableParam>.Builder inlinableParams)
    {
        var nonPartial = FindNonPartialEnclosingType(declaringType);
        if (nonPartial is not null)
        {
            return InlinerSymbolInfo.CreateDiagnostic(
                "SPIRE023",
                $"The type containing '[Inlinable]' parameter must be declared 'partial'; '{nonPartial.Name}' is not partial",
                InlinerSymbolInfo.GetSymbolLocation(method));
        }

        if (inlinableParams.Count == 0)
            return null;

        var bodyRoot = (SyntaxNode?)mds.Body ?? mds.ExpressionBody;
        if (bodyRoot is null)
            return null;

        var parameterNames = new HashSet<string>(
            inlinableParams.Select(parameter => parameter.Name),
            System.StringComparer.Ordinal);
        var trackedNames = new HashSet<string>(
            parameterNames,
            System.StringComparer.Ordinal);

        var aliases = bodyRoot.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .ToList();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var variable in aliases)
            {
                if (variable.Parent is not VariableDeclarationSyntax declaration ||
                    declaration.Type is not IdentifierNameSyntax type ||
                    type.Identifier.Text != "var" ||
                    declaration.Variables.Count != 1 ||
                    variable.Initializer is null)
                {
                    continue;
                }

                var initializer = variable.Initializer.Value;
                var isAlias = initializer is IdentifierNameSyntax rhs &&
                    trackedNames.Contains(rhs.Identifier.ValueText);
                if (initializer is ConditionalExpressionSyntax conditional &&
                    conditional.WhenTrue is IdentifierNameSyntax whenTrue &&
                    conditional.WhenFalse is IdentifierNameSyntax whenFalse)
                {
                    isAlias = trackedNames.Contains(whenTrue.Identifier.ValueText) &&
                        trackedNames.Contains(whenFalse.Identifier.ValueText);
                }

                if (isAlias && trackedNames.Add(variable.Identifier.ValueText))
                    changed = true;
            }
        }

        foreach (var identifier in bodyRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var name = identifier.Identifier.ValueText;
            if (!trackedNames.Contains(name) || IsInsideNameof(identifier))
                continue;

            if (IsInsideNestedFunction(identifier, bodyRoot))
            {
                return InlinerSymbolInfo.CreateDiagnostic(
                    "SPIRE021",
                    $"[Inlinable] parameter '{name}' is captured by a nested function and cannot be rewritten",
                    identifier.GetLocation());
            }

            if (IsAllowedUsage(identifier, trackedNames))
                continue;

            return InlinerSymbolInfo.CreateDiagnostic(
                "SPIRE021",
                $"[Inlinable] parameter '{name}' is used in a form the twin generator cannot rewrite",
                identifier.GetLocation());
        }

        return null;
    }

    private static bool IsAllowedUsage(
        IdentifierNameSyntax identifier,
        HashSet<string> trackedNames)
    {
        var parent = identifier.Parent;
        if (parent is null)
            return false;

        if (parent is InvocationExpressionSyntax invocation &&
            invocation.Expression == identifier)
        {
            return true;
        }

        if (parent is ConditionalAccessExpressionSyntax conditional &&
            conditional.Expression == identifier)
        {
            return true;
        }

        if (parent is ArgumentSyntax)
            return true;

        if (parent is ConditionalExpressionSyntax aliasConditional &&
            aliasConditional.WhenTrue is IdentifierNameSyntax aliasWhenTrue &&
            aliasConditional.WhenFalse is IdentifierNameSyntax aliasWhenFalse &&
            trackedNames.Contains(aliasWhenTrue.Identifier.ValueText) &&
            trackedNames.Contains(aliasWhenFalse.Identifier.ValueText) &&
            aliasConditional.Parent is EqualsValueClauseSyntax aliasEqualsValue &&
            aliasEqualsValue.Parent is VariableDeclaratorSyntax aliasVariable &&
            aliasVariable.Parent is VariableDeclarationSyntax aliasDeclaration &&
            aliasDeclaration.Type is IdentifierNameSyntax aliasType &&
            aliasType.Identifier.Text == "var" &&
            aliasDeclaration.Variables.Count == 1 &&
            trackedNames.Contains(aliasVariable.Identifier.ValueText))
        {
            return true;
        }

        if (parent is BinaryExpressionSyntax binary &&
            (binary.IsKind(SyntaxKind.EqualsExpression) ||
             binary.IsKind(SyntaxKind.NotEqualsExpression)))
        {
            var other = binary.Left == identifier ? binary.Right : binary.Left;
            if (other is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return true;
            }
        }

        if (parent is IsPatternExpressionSyntax pattern &&
            pattern.Expression == identifier)
        {
            return true;
        }

        if (parent is BinaryExpressionSyntax isExpression &&
            isExpression.IsKind(SyntaxKind.IsExpression) &&
            isExpression.Left == identifier)
        {
            return true;
        }

        if (parent is EqualsValueClauseSyntax equalsValue &&
            equalsValue.Parent is VariableDeclaratorSyntax variable &&
            variable.Initializer == equalsValue &&
            variable.Parent is VariableDeclarationSyntax declaration &&
            declaration.Type is IdentifierNameSyntax type &&
            type.Identifier.Text == "var" &&
            declaration.Variables.Count == 1 &&
            trackedNames.Contains(variable.Identifier.ValueText))
        {
            return true;
        }

        if (parent is AssignmentExpressionSyntax assignment &&
            assignment.Left == identifier)
        {
            return false;
        }

        return false;
    }

    private static bool IsInsideNestedFunction(IdentifierNameSyntax identifier, SyntaxNode bodyRoot)
    {
        for (SyntaxNode? current = identifier.Parent;
             current is not null && current != bodyRoot;
             current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax ||
                current is LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideNameof(IdentifierNameSyntax identifier)
    {
        for (SyntaxNode? current = identifier.Parent;
             current is not null;
             current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation &&
                invocation.Expression is IdentifierNameSyntax target &&
                target.Identifier.Text == "nameof")
            {
                return true;
            }

            if (current is StatementSyntax)
                break;
        }

        return false;
    }

    private static INamedTypeSymbol? FindNonPartialEnclosingType(INamedTypeSymbol declaringType)
    {
        for (INamedTypeSymbol? current = declaringType;
             current is not null;
             current = current.ContainingType)
        {
            if (!IsDeclaredPartial(current))
                return current;
        }

        return null;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol type)
    {
        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration)
                continue;

            if (declaration.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }



    private static string BuildUniqueGenericName(string paramName, HashSet<string> used)
    {
        var pascal = ToPascalCase(paramName);
        var baseName = "T" + pascal + "Inliner";
        if (!used.Contains(baseName))
            return baseName;
        for (int n = 1; ; n++)
        {
            var candidate = baseName + n;
            if (!used.Contains(candidate))
                return candidate;
        }
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Strip leading underscores.
        int start = 0;
        while (start < name.Length && name[start] == '_') start++;
        if (start >= name.Length) return name;
        var sb = new StringBuilder(name.Length - start);
        sb.Append(char.ToUpperInvariant(name[start]));
        sb.Append(name, start + 1, name.Length - start - 1);
        return sb.ToString();
    }

    private static HostParamInfo BuildHostParamInfo(
        IParameterSymbol p,
        int position,
        bool isThis,
        string? sourceType = null)
    {
        string? refKind = p.IsParams
            ? "params"
            : p.RefKind switch
            {
                RefKind.Ref => "ref",
                RefKind.Out => "out",
                RefKind.In => "in",
                _ when RoslynCompatibility.IsRefReadOnlyParameter(p.RefKind) => "ref readonly",
                _ => null,
            };

        return new HostParamInfo(
            Name: p.Name,
            Type: sourceType ?? DisplayType(p.Type),
            Position: position,
            RefKind: refKind,
            IsThis: isThis);
    }



}
