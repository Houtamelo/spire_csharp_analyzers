using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerStructParser
{
    public static InlinerStructDecl? Parse(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method)
            return null;

        var declaringType = method.ContainingType;
        if (declaringType is null)
            return null;

        var ns = declaringType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : declaringType.ContainingNamespace.ToDisplayString();

        var containingTypes = BuildContainingTypes(declaringType);

        var declaringTypeName = declaringType.Name;
        var declaringTypeKeyword = GetTypeKeyword(declaringType);
        var declaringTypeAccessibility = AccessibilityToKeyword(declaringType.DeclaredAccessibility);
        var declaringTypeParameters = new EquatableArray<string>(
            declaringType.TypeParameters.Select(tp => tp.Name).ToImmutableArray());

        var isVoid = method.ReturnsVoid;
        var returnType = isVoid
            ? null
            : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var parameters = new EquatableArray<InlinerParamInfo>(
            method.Parameters
                .Select(p => new InlinerParamInfo(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                .ToImmutableArray());

        var typeParameters = new EquatableArray<string>(
            method.TypeParameters.Select(tp => tp.Name).ToImmutableArray());

        // Task 14 will populate constraints; leave empty for this slice.
        var typeParameterConstraints = new EquatableArray<string>(ImmutableArray<string>.Empty);

        return new InlinerStructDecl(
            Namespace: ns,
            MethodName: method.Name,
            Accessibility: AccessibilityToKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            IsVoid: isVoid,
            ReturnType: returnType,
            DeclaringTypeName: declaringTypeName,
            DeclaringTypeKeyword: declaringTypeKeyword,
            DeclaringTypeAccessibility: declaringTypeAccessibility,
            DeclaringTypeParameters: declaringTypeParameters,
            ContainingTypes: new EquatableArray<InlinerContainingType>(containingTypes),
            TypeParameters: typeParameters,
            TypeParameterConstraints: typeParameterConstraints,
            Parameters: parameters,
            Diagnostic: null);
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
        chain.Reverse(); // outermost first
        return chain.ToImmutableArray();
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
            return "interface";

        if (type.IsRecord && type.IsValueType)
            return "record struct";

        if (type.IsRecord)
            return "record";

        if (type.IsValueType)
        {
            // ref struct / readonly ref struct / readonly struct
            if (type.IsReadOnly)
                return "readonly struct";
            return "struct";
        }

        // Reference type (class)
        if (type.IsStatic)
            return "static class";

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
