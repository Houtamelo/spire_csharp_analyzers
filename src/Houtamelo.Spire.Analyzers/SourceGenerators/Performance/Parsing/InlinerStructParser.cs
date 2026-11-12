using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators;
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

        var containingTypes = InlinerSymbolInfo.BuildContainingTypes(declaringType);

        var declaringTypeName = declaringType.Name;
        var declaringTypeKeyword = InlinerSymbolInfo.GetTypeKeyword(declaringType);
        var declaringTypeAccessibility = InlinerSymbolInfo.AccessibilityToKeyword(declaringType.DeclaredAccessibility);
        var declaringTypeParameters = new EquatableArray<string>(
            declaringType.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray());
        var declaringTypeParameterConstraints =
            InlinerSymbolInfo.BuildTypeParameterConstraints(declaringType);

        var isVoid = method.ReturnsVoid;
        var returnType = isVoid
            ? null
            : DisplayType(method.ReturnType);

        var parameters = new EquatableArray<InlinerParamInfo>(
            method.Parameters
                .Select(p => new InlinerParamInfo(
                    p.Name,
                    DisplayType(p.Type)))
                .ToImmutableArray());

        var typeParameters = new EquatableArray<string>(
            method.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray());

        var typeParameterConstraints = InlinerSymbolInfo.BuildMethodTypeParameterConstraints(method);

        var diagnostic = ValidateMethod(method, declaringType);

        return new InlinerStructDecl(
            Namespace: ns,
            MethodName: method.Name,
            Accessibility: InlinerSymbolInfo.AccessibilityToKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            IsVoid: isVoid,
            ReturnType: returnType,
            DeclaringTypeName: declaringTypeName,
            DeclaringTypeKeyword: declaringTypeKeyword,
            DeclaringTypeAccessibility: declaringTypeAccessibility,
            DeclaringTypeParameters: declaringTypeParameters,
            DeclaringTypeParameterConstraints: declaringTypeParameterConstraints,
            ContainingTypes: new EquatableArray<InlinerContainingType>(containingTypes),
            TypeParameters: typeParameters,
            TypeParameterConstraints: typeParameterConstraints,
            Parameters: parameters,
            HintName: InlinerHintNaming.ForInlinerStruct(method),
            HasExperimentalAttribute: CompilationInfo.HasAccessibleType(
                ctx.SemanticModel.Compilation, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute"),
            Diagnostic: diagnostic);
    }

    private static string DisplayType(ITypeSymbol type)
        => type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    private static InlinerDiagnostic? ValidateMethod(IMethodSymbol method, INamedTypeSymbol declaringType)
    {
        // SPIRE018: declaring type is a ref struct.
        if (declaringType.IsRefLikeType)
            return InlinerSymbolInfo.CreateDiagnostic("SPIRE018",
                "[InlinerStruct] cannot be applied to a method declared on a ref struct",
                InlinerSymbolInfo.GetSymbolLocation(method));

        // SPIRE027: declaring type and all enclosing types must be declared partial.
        var nonPartialType = FindNonPartialType(declaringType);
        if (nonPartialType is not null)
            return InlinerSymbolInfo.CreateDiagnostic("SPIRE027",
                $"Type '{nonPartialType.Name}' must be declared 'partial' for inliner-struct generation",
                InlinerSymbolInfo.GetSymbolLocation(method));

        // SPIRE017: parameters cannot have ref/in/out/ref readonly/params modifiers.
        foreach (var p in method.Parameters)
        {
            var modifier = GetUnsupportedModifier(p);
            if (modifier is not null)
                return InlinerSymbolInfo.CreateDiagnostic("SPIRE017",
                    $"Parameter '{p.Name}' uses an unsupported modifier ({modifier}); [InlinerStruct] methods cannot have ref/in/out/ref readonly/params parameters",
                    InlinerSymbolInfo.GetSymbolLocation(p));
        }

        // SPIRE019: total arity must not exceed 8. Instance methods count the receiver.
        var arity = method.Parameters.Length + (method.IsStatic ? 0 : 1);
        if (arity > MaxArity)
            return InlinerSymbolInfo.CreateDiagnostic("SPIRE019",
                $"Method '{method.Name}' has arity {arity} (instance methods count the receiver), which exceeds the supported maximum of 8",
                InlinerSymbolInfo.GetSymbolLocation(method));

        // SPIRE020: generated struct name collides with an existing member of the declaring type.
        var structName = method.Name + "Inliner";
        var collisions = declaringType.GetMembers(structName);
        if (!collisions.IsDefaultOrEmpty && collisions.Length > 0)
            return InlinerSymbolInfo.CreateDiagnostic("SPIRE020",
                $"Generated struct '{structName}' collides with an existing type in the same namespace/nesting",
                InlinerSymbolInfo.GetSymbolLocation(method));

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
            _ when RoslynCompatibility.IsRefReadOnlyParameter(p.RefKind) => "ref readonly",
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






}
