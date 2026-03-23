using System.Collections.Generic;
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

        var isRefStruct = typeSymbol.IsRefLikeType;

        var layout = GetLayout(ctx.Attributes);
        var isGeneric = typeSymbol.TypeParameters.Length > 0;
        var strategy = ResolveStrategy(declKind, layout, isGeneric);

        // Reject Overlap on generic struct (CLR restriction)
        if (strategy == EmitStrategy.Overlap && isGeneric)
        {
            return new UnionDeclaration(
                Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : typeSymbol.ContainingNamespace.ToDisplayString(),
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: ExplicitAccessibility(syntax, typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                IsRefStruct: isRefStruct,
                Strategy: strategy,
                GenerateDeconstruct: true,
                TypeParameters: new EquatableArray<string>(typeSymbol.TypeParameters
                    .Select(tp => tp.Name).ToImmutableArray()),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                ContainingTypes: new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty),
                Diagnostic: CreateDiagnostic(
                    syntax,
                    "SPIRE_DU005",
                    "Generic structs cannot use Overlap layout (CLR restriction); use BoxedFields or BoxedTuple",
                    isError: true),
                Json: JsonLibrary.None,
                JsonDiscriminator: "kind",
                HasInitProperties: false);
        }

        // Warn when Layout is explicitly set on record/class (it's ignored)
        UnionDiagnostic? layoutWarning = null;
        if ((declKind == "record" || declKind == "class") && layout != 0)
        {
            layoutWarning = CreateDiagnostic(
                syntax,
                "SPIRE_DU004",
                "Layout parameter is ignored for record/class discriminated unions",
                isError: false);
        }

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
                AccessibilityKeyword: ExplicitAccessibility(syntax, typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                IsRefStruct: isRefStruct,
                Strategy: strategy,
                GenerateDeconstruct: true,
                TypeParameters: new EquatableArray<string>(typeSymbol.TypeParameters
                    .Select(tp => tp.Name).ToImmutableArray()),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                ContainingTypes: new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty),
                Diagnostic: CreateDiagnostic(
                    syntax,
                    "SPIRE_DU003",
                    hint,
                    isError: false),
                Json: JsonLibrary.None,
                JsonDiscriminator: "kind",
                HasInitProperties: false);
        }

        var json = GetJsonLibrary(ctx.Attributes);
        // ref struct + JSON: report error but don't block union generation
        // (isError: false so the generator still emits the union source)
        if (isRefStruct && json != JsonLibrary.None)
        {
            layoutWarning = CreateDiagnostic(
                syntax,
                "SPIRE_DU008",
                "ref struct cannot use JSON generation (ref structs cannot be generic type arguments)",
                isError: false);
            json = JsonLibrary.None;
        }

        var typeParams = typeSymbol.TypeParameters
            .Select(tp => tp.Name)
            .ToImmutableArray();

        return new UnionDeclaration(
            Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            AccessibilityKeyword: ExplicitAccessibility(syntax, typeSymbol.DeclaredAccessibility),
            DeclarationKeyword: declKind,
            IsReadonly: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
            IsRefStruct: isRefStruct,
            Strategy: strategy,
            GenerateDeconstruct: GetGenerateDeconstruct(ctx.Attributes),
            TypeParameters: new EquatableArray<string>(typeParams),
            Variants: new EquatableArray<VariantInfo>(variants),
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(GetContainingTypes(typeSymbol)),
            Diagnostic: layoutWarning,
            Json: json,
            JsonDiscriminator: GetJsonDiscriminator(ctx.Attributes),
            HasInitProperties: false);
    }

    /// Walks up the ContainingType chain and returns the nesting wrappers
    /// from outermost to innermost.
    private static ImmutableArray<ContainingTypeInfo> GetContainingTypes(INamedTypeSymbol typeSymbol)
    {
        var chain = new List<ContainingTypeInfo>();
        var current = typeSymbol.ContainingType;
        while (current is not null)
        {
            string keyword;
            if (current.IsStatic)
                keyword = "static class";
            else if (current.IsRecord && current.IsValueType)
                keyword = "record struct";
            else if (current.IsRecord)
                keyword = "record";
            else if (current.IsValueType)
                keyword = "struct";
            else
                keyword = "class";

            chain.Add(new ContainingTypeInfo(
                AccessibilityKeyword: AccessibilityToKeyword(current.DeclaredAccessibility),
                Keyword: keyword,
                Name: current.Name));
            current = current.ContainingType;
        }
        chain.Reverse(); // outermost first
        return chain.ToImmutableArray();
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

    /// Reads the GenerateDeconstruct named property (defaults to true).
    private static bool GetGenerateDeconstruct(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != "DiscriminatedUnionAttribute")
                continue;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "GenerateDeconstruct" && named.Value.Value is bool val)
                    return val;
            }
        }

        return true;
    }

    /// Reads the Json flags enum named property (defaults to None).
    private static JsonLibrary GetJsonLibrary(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != "DiscriminatedUnionAttribute")
                continue;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "Json" && named.Value.Value is int val)
                    return (JsonLibrary)val;
            }
        }

        return JsonLibrary.None;
    }

    /// Reads the JsonDiscriminator named property (defaults to "kind").
    private static string GetJsonDiscriminator(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != "DiscriminatedUnionAttribute")
                continue;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "JsonDiscriminator" && named.Value.Value is string val)
                    return val;
            }
        }

        return "kind";
    }

    /// Reads [JsonName] attribute from a symbol and returns the Name value, or null.
    private static string? GetJsonName(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JsonNameAttribute")
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string name)
            {
                return name;
            }
        }

        return null;
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
            4 => EmitStrategy.Additive,    // Additive
            5 => EmitStrategy.UnsafeOverlap, // UnsafeOverlap
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

    /// Record copy constructor: implicitly declared, single parameter of the record's own type.
    private static bool IsCopyConstructor(IMethodSymbol ctor, INamedTypeSymbol containingType)
    {
        return ctor.IsImplicitlyDeclared
            && ctor.Parameters.Length == 1
            && SymbolEqualityComparer.Default.Equals(
                ctor.Parameters[0].Type, containingType);
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
                KnownSize: ComputeKnownSize(p.Type),
                JsonName: GetJsonName(p)))
            .ToImmutableArray();

        return new VariantInfo(
            Name: method.Name,
            Fields: new EquatableArray<FieldInfo>(fields),
            JsonName: GetJsonName(method),
            AccessibilityKeyword: "public");
    }

    /// Extracts variant info from a nested type's primary constructor parameters.
    private static VariantInfo ParseNestedTypeVariant(INamedTypeSymbol nestedType)
    {
        // Find the primary constructor (the one with parameters, or the parameterless one).
        // For positional records `record Some(T Value)`, the compiler generates a ctor
        // with matching parameters.
        // Exclude the record copy constructor (implicitly declared, single param of own type).
        var primaryCtor = nestedType.Constructors
            .Where(c => !IsCopyConstructor(c, nestedType))
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
                    KnownSize: ComputeKnownSize(p.Type),
                    JsonName: GetJsonName(p)))
                .ToImmutableArray();

        return new VariantInfo(
            Name: nestedType.Name,
            Fields: new EquatableArray<FieldInfo>(fields),
            JsonName: GetJsonName(nestedType),
            AccessibilityKeyword: ExplicitAccessibility(nestedType));
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

    private static UnionDiagnostic CreateDiagnostic(
        TypeDeclarationSyntax syntax, string id, string message, bool isError)
    {
        var location = syntax.Identifier.GetLocation();
        var lineSpan = location.GetLineSpan();
        return new UnionDiagnostic(
            Id: id,
            Message: message,
            IsError: isError,
            FilePath: lineSpan.Path ?? "",
            StartOffset: location.SourceSpan.Start,
            Length: location.SourceSpan.Length,
            StartLine: lineSpan.StartLinePosition.Line,
            StartColumn: lineSpan.StartLinePosition.Character,
            EndLine: lineSpan.EndLinePosition.Line,
            EndColumn: lineSpan.EndLinePosition.Character);
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

    /// Returns the accessibility keyword only if the user explicitly wrote one.
    /// Empty string if accessibility was implicit (C# default).
    private static string ExplicitAccessibility(TypeDeclarationSyntax syntax, Accessibility declared)
    {
        bool hasExplicit = syntax.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.PrivateKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword));
        return hasExplicit ? AccessibilityToKeyword(declared) : "";
    }

    /// Returns the accessibility keyword for a symbol, only if explicit in source.
    private static string ExplicitAccessibility(INamedTypeSymbol symbol)
    {
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeSyntax)
                return ExplicitAccessibility(typeSyntax, symbol.DeclaredAccessibility);
        }
        return AccessibilityToKeyword(symbol.DeclaredAccessibility);
    }
}
