using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinableParser
{
    private const string InlinableAttrFullName = "Houtamelo.Spire.InlinableAttribute";

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
                    otherParams.Add(BuildHostParamInfo(p, i, isThis));
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

        var containingTypes = BuildContainingTypes(declaringType);

        var declaringTypeParameters = new EquatableArray<string>(
            declaringType.TypeParameters.Select(tp => tp.Name).ToImmutableArray());

        var returnType = method.ReturnsVoid
            ? "void"
            : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var hostTypeParameters = new EquatableArray<string>(
            method.TypeParameters.Select(tp => tp.Name).ToImmutableArray());
        var hostConstraints = new EquatableArray<string>(
            BuildConstraints(mds).ToImmutableArray());

        // Body text: block body or expression body.
        string bodyText;
        if (mds.Body is not null)
            bodyText = mds.Body.ToFullString();
        else if (mds.ExpressionBody is not null)
            bodyText = mds.ExpressionBody.ToFullString();
        else
            bodyText = "";

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
            Accessibility: AccessibilityToKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            ReturnType: returnType,
            DeclaringTypeName: declaringType.Name,
            DeclaringTypeKeyword: GetTypeKeyword(declaringType),
            DeclaringTypeAccessibility: AccessibilityToKeyword(declaringType.DeclaredAccessibility),
            DeclaringTypeParameters: declaringTypeParameters,
            ContainingTypes: new EquatableArray<InlinerContainingType>(containingTypes),
            HostTypeParameters: hostTypeParameters,
            HostTypeParameterConstraints: hostConstraints,
            InlinableParams: new EquatableArray<InlinableParam>(inlinableParams.ToImmutable()),
            OtherParams: new EquatableArray<HostParamInfo>(otherParams.ToImmutable()),
            OriginalBody: bodyText,
            UsingDirectives: new EquatableArray<string>(usings.ToImmutableArray()),
            Diagnostic: null);
    }

    private static bool TryBuildInlinable(
        IParameterSymbol p,
        int position,
        HashSet<string> usedGenericNames,
        out InlinableParam? result)
    {
        result = null;
        var type = p.Type;

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
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
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

    private static HostParamInfo BuildHostParamInfo(IParameterSymbol p, int position, bool isThis)
    {
        string? refKind = p.IsParams
            ? "params"
            : p.RefKind switch
            {
                RefKind.Ref => "ref",
                RefKind.Out => "out",
                RefKind.In => "in",
                RefKind.RefReadOnlyParameter => "ref readonly",
                _ => null,
            };

        return new HostParamInfo(
            Name: p.Name,
            Type: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Position: position,
            RefKind: refKind,
            IsThis: isThis);
    }

    private static ImmutableArray<InlinerContainingType> BuildContainingTypes(INamedTypeSymbol declaringType)
    {
        var chain = new List<InlinerContainingType>();
        var current = declaringType.ContainingType;
        while (current is not null)
        {
            chain.Add(new InlinerContainingType(
                AccessibilityKeyword: AccessibilityToKeyword(current.DeclaredAccessibility),
                Keyword: GetTypeKeyword(current),
                Name: current.Name,
                TypeParameters: new EquatableArray<string>(
                    current.TypeParameters.Select(tp => tp.Name).ToImmutableArray())));
            current = current.ContainingType;
        }
        chain.Reverse();
        return chain.ToImmutableArray();
    }

    private static IEnumerable<string> BuildConstraints(MethodDeclarationSyntax mds)
    {
        foreach (var clause in mds.ConstraintClauses)
        {
            yield return clause.ToString().Trim();
        }
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface) return "interface";
        if (type.IsRecord && type.IsValueType) return "record struct";
        if (type.IsRecord) return "record";
        if (type.IsValueType)
        {
            if (type.IsReadOnly) return "readonly struct";
            return "struct";
        }
        if (type.IsStatic) return "static class";
        return "class";
    }

    private static string AccessibilityToKeyword(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };
    }
}
