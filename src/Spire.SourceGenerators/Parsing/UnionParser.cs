using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators.Parsing;

internal static class UnionParser
{
    public static UnionDeclaration? Parse(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var syntax = (TypeDeclarationSyntax)ctx.TargetNode;
        var declKind = GetDeclarationKind(syntax);
        if (declKind is null) return null;

        // Reject nested types
        if (typeSymbol.ContainingType is not null)
        {
            return new UnionDeclaration(
                Namespace: "",
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: false,
                Strategy: EmitStrategy.Overlap,
                TypeParameters: new EquatableArray<string>(ImmutableArray<string>.Empty),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                Diagnostic: new UnionDiagnostic(
                    "SPIRE_DU001",
                    "Nested type declarations are not supported for [DiscriminatedUnion]",
                    IsError: true));
        }

        // Reject ref structs
        if (typeSymbol.IsRefLikeType)
        {
            return new UnionDeclaration(
                Namespace: "",
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: false,
                Strategy: EmitStrategy.Overlap,
                TypeParameters: new EquatableArray<string>(ImmutableArray<string>.Empty),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                Diagnostic: new UnionDiagnostic(
                    "SPIRE_DU002",
                    "ref struct is not supported for [DiscriminatedUnion]",
                    IsError: true));
        }

        var layout = GetLayout(ctx.Attributes);
        var isGeneric = typeSymbol.TypeParameters.Length > 0;
        var strategy = ResolveStrategy(declKind, layout, isGeneric);

        // Record/class paths discover variants as nested types inheriting from the parent.
        // Struct paths discover variants as [Variant] static methods.
        var variants = (declKind == "record" || declKind == "class")
            ? DiscoverNestedTypeVariants(typeSymbol)
            : DiscoverMethodVariants(typeSymbol);

        if (variants.Length == 0)
        {
            var hint = (declKind == "record" || declKind == "class")
                ? "No nested variant types found inheriting from the union type."
                : "No [Variant] methods found on discriminated union type.";
            return new UnionDeclaration(
                Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : typeSymbol.ContainingNamespace.ToDisplayString(),
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                Strategy: strategy,
                TypeParameters: new EquatableArray<string>(typeSymbol.TypeParameters
                    .Select(tp => tp.Name).ToImmutableArray()),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                Diagnostic: new UnionDiagnostic(
                    "SPIRE_DU003",
                    hint,
                    IsError: false));
        }

        var typeParams = typeSymbol.TypeParameters
            .Select(tp => tp.Name)
            .ToImmutableArray();

        return new UnionDeclaration(
            Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
            DeclarationKeyword: declKind,
            IsReadonly: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
            Strategy: strategy,
            TypeParameters: new EquatableArray<string>(typeParams),
            Variants: new EquatableArray<VariantInfo>(variants),
            Diagnostic: null);
    }

    /// Returns "struct", "record", or "class".
    private static string? GetDeclarationKind(TypeDeclarationSyntax syntax)
    {
        return syntax switch
        {
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax record =>
                record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                    ? "struct"
                    : "record",
            ClassDeclarationSyntax => "class",
            _ => null,
        };
    }

    /// Reads Layout enum value from the [DiscriminatedUnion] attribute constructor arg.
    private static int GetLayout(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != "DiscriminatedUnionAttribute")
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is int layoutValue)
            {
                return layoutValue;
            }
        }

        return 0; // Layout.Auto
    }

    /// Maps (declarationKind, layout, isGeneric) to EmitStrategy.
    private static EmitStrategy ResolveStrategy(string declKind, int layout, bool isGeneric)
    {
        if (declKind == "record") return EmitStrategy.Record;
        if (declKind == "class") return EmitStrategy.Class;

        // struct paths
        return layout switch
        {
            0 => isGeneric ? EmitStrategy.BoxedFields : EmitStrategy.Overlap, // Auto
            1 => EmitStrategy.Overlap,     // Overlap
            2 => EmitStrategy.BoxedFields, // BoxedFields
            3 => EmitStrategy.BoxedTuple,  // BoxedTuple
            _ => EmitStrategy.Overlap,
        };
    }

    /// Discovers variants from [Variant] static methods (struct path).
    private static ImmutableArray<VariantInfo> DiscoverMethodVariants(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && HasVariantAttribute(m))
            .Select(m => ParseMethodVariant(m))
            .ToImmutableArray();
    }

    /// Discovers variants from nested types that inherit from the parent (record/class path).
    private static ImmutableArray<VariantInfo> DiscoverNestedTypeVariants(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetTypeMembers()
            .Where(nested => SymbolEqualityComparer.Default.Equals(
                nested.BaseType?.OriginalDefinition, typeSymbol.OriginalDefinition))
            .Select(nested => ParseNestedTypeVariant(nested))
            .ToImmutableArray();
    }

    private static bool HasVariantAttribute(IMethodSymbol method)
    {
        return method.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "VariantAttribute");
    }

    private static VariantInfo ParseMethodVariant(IMethodSymbol method)
    {
        var fields = method.Parameters
            .Select(p => new FieldInfo(
                Name: p.Name,
                TypeFullName: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsUnmanaged: p.Type.IsUnmanagedType,
                IsReferenceType: p.Type.IsReferenceType,
                KnownSize: ComputeKnownSize(p.Type)))
            .ToImmutableArray();

        return new VariantInfo(
            Name: method.Name,
            Fields: new EquatableArray<FieldInfo>(fields));
    }

    /// Extracts variant info from a nested type's primary constructor parameters.
    private static VariantInfo ParseNestedTypeVariant(INamedTypeSymbol nestedType)
    {
        // Find the primary constructor (the one with parameters, or the parameterless one).
        // For positional records `record Some(T Value)`, the compiler generates a ctor
        // with matching parameters.
        var primaryCtor = nestedType.Constructors
            .Where(c => !c.IsImplicitlyDeclared || c.Parameters.Length > 0)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        var fields = primaryCtor is null
            ? ImmutableArray<FieldInfo>.Empty
            : primaryCtor.Parameters
                .Select(p => new FieldInfo(
                    Name: p.Name,
                    TypeFullName: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsUnmanaged: p.Type.IsUnmanagedType,
                    IsReferenceType: p.Type.IsReferenceType,
                    KnownSize: ComputeKnownSize(p.Type)))
                .ToImmutableArray();

        return new VariantInfo(
            Name: nestedType.Name,
            Fields: new EquatableArray<FieldInfo>(fields));
    }

    /// Returns sizeof for primitives, enums (underlying type), bool, nint/nuint.
    /// Null for everything else. Called at parse time while semantic model is available.
    private static int? ComputeKnownSize(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            var underlying = ((INamedTypeSymbol)type).EnumUnderlyingType;
            if (underlying is not null)
                return ComputeKnownSize(underlying);
            return null;
        }

        return type.SpecialType switch
        {
            SpecialType.System_Byte => 1,
            SpecialType.System_SByte => 1,
            SpecialType.System_Boolean => 1,
            SpecialType.System_Int16 => 2,
            SpecialType.System_UInt16 => 2,
            SpecialType.System_Char => 2,
            SpecialType.System_Int32 => 4,
            SpecialType.System_UInt32 => 4,
            SpecialType.System_Single => 4,
            SpecialType.System_Int64 => 8,
            SpecialType.System_UInt64 => 8,
            SpecialType.System_Double => 8,
            SpecialType.System_IntPtr => 8,
            SpecialType.System_UIntPtr => 8,
            _ => null,
        };
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
