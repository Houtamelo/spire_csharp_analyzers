using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerStructParser
{
    private const int MaxArity = 8;

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

        var diagnostic = ValidateMethod(method, declaringType);

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
            Diagnostic: diagnostic);
    }

    private static InlinerDiagnostic? ValidateMethod(IMethodSymbol method, INamedTypeSymbol declaringType)
    {
        // SPIRE018: declaring type is a ref struct.
        if (declaringType.IsRefLikeType)
            return CreateDiagnostic("SPIRE018",
                "[InlinerStruct] cannot be applied to a method declared on a ref struct",
                GetSymbolLocation(method));

        // SPIRE027: declaring type and all enclosing types must be declared partial.
        var nonPartialType = FindNonPartialType(declaringType);
        if (nonPartialType is not null)
            return CreateDiagnostic("SPIRE027",
                $"Type '{nonPartialType.Name}' must be declared 'partial' for inliner-struct generation",
                GetSymbolLocation(method));

        // SPIRE017: parameters cannot have ref/in/out/ref readonly/params modifiers.
        foreach (var p in method.Parameters)
        {
            var modifier = GetUnsupportedModifier(p);
            if (modifier is not null)
                return CreateDiagnostic("SPIRE017",
                    $"Parameter '{p.Name}' uses an unsupported modifier ({modifier}); [InlinerStruct] methods cannot have ref/in/out/ref readonly/params parameters",
                    GetSymbolLocation(p));
        }

        // SPIRE019: total arity must not exceed 8. Instance methods count the receiver.
        var arity = method.Parameters.Length + (method.IsStatic ? 0 : 1);
        if (arity > MaxArity)
            return CreateDiagnostic("SPIRE019",
                $"Method '{method.Name}' has arity {arity} (instance methods count the receiver), which exceeds the supported maximum of 8",
                GetSymbolLocation(method));

        // SPIRE020: generated struct name collides with an existing member of the declaring type.
        var structName = method.Name + "Inliner";
        var collisions = declaringType.GetMembers(structName);
        if (!collisions.IsDefaultOrEmpty && collisions.Length > 0)
            return CreateDiagnostic("SPIRE020",
                $"Generated struct '{structName}' collides with an existing type in the same namespace/nesting",
                GetSymbolLocation(method));

        return null;
    }

    private static string? GetUnsupportedModifier(IParameterSymbol p)
    {
        if (p.IsParams)
            return "params";

        return p.RefKind switch
        {
            RefKind.Ref => "ref",
            RefKind.Out => "out",
            RefKind.In => "in",
            RefKind.RefReadOnlyParameter => "ref readonly",
            _ => null,
        };
    }

    private static INamedTypeSymbol? FindNonPartialType(INamedTypeSymbol declaringType)
    {
        // Walk from declaring type up through its enclosing types; return the first
        // type whose declarations are none-partial. A partial type only needs ONE
        // of its declarations to have the partial modifier.
        INamedTypeSymbol? current = declaringType;
        while (current is not null)
        {
            if (!IsDeclaredPartial(current))
                return current;
            current = current.ContainingType;
        }
        return null;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol type)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            if (node is TypeDeclarationSyntax tds)
            {
                foreach (var modifier in tds.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                        return true;
                }
            }
        }
        return false;
    }

    private static InlinerDiagnostic CreateDiagnostic(string id, string message, Location location)
    {
        var lineSpan = location.GetLineSpan();
        var sourceSpan = location.SourceSpan;
        var filePath = location.SourceTree?.FilePath ?? "";

        return new InlinerDiagnostic(
            Id: id,
            Message: message,
            FilePath: filePath,
            StartOffset: sourceSpan.Start,
            Length: sourceSpan.Length,
            StartLine: lineSpan.StartLinePosition.Line,
            StartColumn: lineSpan.StartLinePosition.Character,
            EndLine: lineSpan.EndLinePosition.Line,
            EndColumn: lineSpan.EndLinePosition.Character);
    }

    private static Location GetSymbolLocation(ISymbol symbol)
    {
        var locations = symbol.Locations;
        if (!locations.IsDefaultOrEmpty)
        {
            foreach (var loc in locations)
            {
                if (loc.IsInSource)
                    return loc;
            }
        }
        return Location.None;
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
