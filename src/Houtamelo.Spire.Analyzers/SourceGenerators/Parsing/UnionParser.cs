using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Parsing;

internal static class UnionParser
{
    private const string UnitySerializeReferenceAttributeMetadataName =
        "Houtamelo.Spire.UnitySerializeReferenceAttribute";
    private const string UnityDebugMetadataName = "UnityEngine.Debug";

    public static UnionDeclaration? Parse(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var syntax = (TypeDeclarationSyntax)ctx.TargetNode;
        var declKind = GetDeclarationKind(syntax);

        var rawConfig = ReadAttributeArgs(ctx.Attributes);
        var isGeneric = typeSymbol.TypeParameters.Length > 0;
        var isUnitySerializable = UnitySerializationValidation.HasSerializableAttribute(typeSymbol);
        var useUnityAssertions = HasUsableUnityDebugAssert(ctx.SemanticModel.Compilation);
        var unitySerializeReferenceAttribute = ctx.SemanticModel.Compilation.GetTypeByMetadataName(
            UnitySerializeReferenceAttributeMetadataName);

        if (declKind is null)
        {
            return new UnionDeclaration(
                Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : typeSymbol.ContainingNamespace.ToDisplayString(),
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: ExplicitAccessibility(syntax, typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: "class",
                IsReadonly: false,
                IsRefStruct: false,
                Strategy: EmitStrategy.VariantPayload,
                TypeParameters: new EquatableArray<string>(ImmutableArray<string>.Empty),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                ContainingTypes: new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty),
                Diagnostic: CreateDiagnostic(
                    syntax,
                    "SPIRE_DU011",
                    "[DiscriminatedUnion] is not supported on class types. Consider using a record type (fully supported by the generator), C# 15 'union' types for a language-native alternative, or the [EnforceExhaustiveness] attribute for exhaustive switch checking on existing class hierarchies",
                    isError: true),
                Json: JsonLibrary.None,
                JsonDiscriminator: "kind",
                HasInitProperties: false,
                RawConfig: rawConfig,
                IsGeneric: isGeneric);
        }

        var isRefStruct = typeSymbol.IsRefLikeType;

        var strategy = declKind == "record" ? EmitStrategy.Record : EmitStrategy.VariantPayload;
        UnionDiagnostic? declarationDiagnostic = null;
        var genericAncestor = NearestGenericContainingType(typeSymbol);
        if (declKind == "struct" && genericAncestor is not null)
        {
            declarationDiagnostic = CreateDiagnostic(
                syntax,
                "SPIRE_DU018",
                $"Discriminated Unions require using `[StructLayout(LayoutKind.Explicit)]` internally, which C# forbids inside generic types, please move the type `{typeSymbol.Name}` to outside the type `{genericAncestor.Name}`. Note: The type {typeSymbol.Name} itself can be generic, it just can't be placed inside another generic type such as `{genericAncestor.Name}`.",
                isError: true);
        }

        var parsedVariants = declKind == "record"
            ? DiscoverNestedTypeVariants(
                typeSymbol,
                isUnitySerializable,
                unitySerializeReferenceAttribute)
            : DiscoverMethodVariants(
                typeSymbol,
                includeNullableReferenceTypeModifiers: true,
                unitySerializeReferenceAttribute);

        var variants = declKind == "record"
            ? parsedVariants
            : FieldNameResolver.ResolveStorageTypes(
                new EquatableArray<VariantInfo>(parsedVariants),
                typeSymbol,
                ctx.SemanticModel.Compilation).ToImmutableArray();

        if (variants.Length == 0)
        {
            var hint = declKind == "record"
                ? "No nested variant types found inheriting from the union type."
                : "No [Variant] methods found on discriminated union type.";
            var noVariantDiagnostic = isUnitySerializable
                ? UnitySerializationValidation.Validate(
                    typeSymbol,
                    syntax,
                    ctx.SemanticModel.Compilation,
                    variants,
                    out _)
                : null;
            return new UnionDeclaration(
                Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : typeSymbol.ContainingNamespace.ToDisplayString(),
                TypeName: typeSymbol.Name,
                AccessibilityKeyword: ExplicitAccessibility(syntax, typeSymbol.DeclaredAccessibility),
                DeclarationKeyword: declKind,
                IsReadonly: typeSymbol.IsReadOnly,
                IsRefStruct: isRefStruct,
                Strategy: strategy,
                TypeParameters: new EquatableArray<string>(typeSymbol.TypeParameters
                    .Select(tp => tp.Name).ToImmutableArray()),
                Variants: new EquatableArray<VariantInfo>(ImmutableArray<VariantInfo>.Empty),
                ContainingTypes: new EquatableArray<ContainingTypeInfo>(GetContainingTypes(typeSymbol)),
                Diagnostic: declarationDiagnostic ?? noVariantDiagnostic ?? CreateDiagnostic(
                    syntax,
                    "SPIRE_DU003",
                    hint,
                    isError: false),
                Json: JsonLibrary.None,
                JsonDiscriminator: "kind",
                HasInitProperties: false,
                RawConfig: rawConfig,
                IsGeneric: isGeneric,
                IsUnitySerializable: isUnitySerializable);
        }

        // Resolve with built-in defaults (will be re-resolved with global config later)
        var json = ResolveJson(rawConfig.Json);

        // ref struct + JSON: report error but don't block union generation
        if (isRefStruct && json != JsonLibrary.None)
        {
            declarationDiagnostic = CreateDiagnostic(
                syntax,
                "SPIRE_DU008",
                "ref struct cannot use JSON generation (ref structs cannot be generic type arguments)",
                isError: false);
            json = JsonLibrary.None;
        }

        var typeParams = typeSymbol.TypeParameters
            .Select(tp => tp.Name)
            .ToImmutableArray();

        UnionDiagnostic? unityDiagnostic = null;
        if (isUnitySerializable)
        {
            unityDiagnostic = UnitySerializationValidation.Validate(
                typeSymbol,
                syntax,
                ctx.SemanticModel.Compilation,
                variants,
                out var invalidAliasVariants);

            if (invalidAliasVariants.Length > 0)
            {
                var invalidNames = new HashSet<string>(
                    invalidAliasVariants,
                    System.StringComparer.Ordinal);
                variants = variants
                    .Select(variant => invalidNames.Contains(variant.Name)
                        ? variant with { FormerlySerializedAsIsValid = false }
                        : variant)
                    .ToImmutableArray();
            }
        }

        return new UnionDeclaration(
            Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
            DeclarationKeyword: declKind,
            IsReadonly: typeSymbol.IsReadOnly,
            IsRefStruct: isRefStruct,
            Strategy: strategy,
            TypeParameters: new EquatableArray<string>(typeParams),
            Variants: new EquatableArray<VariantInfo>(variants),
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(GetContainingTypes(typeSymbol)),
            Diagnostic: declarationDiagnostic ?? unityDiagnostic,
            Json: json,
            JsonDiscriminator: rawConfig.JsonDiscriminator ?? "kind",
            HasInitProperties: false,
            RawConfig: rawConfig,
            IsGeneric: isGeneric,
            IsUnitySerializable: isUnitySerializable,
            UseUnityAssertions: useUnityAssertions)
        {
            ReferenceStorage = ResolveReferenceStorage(rawConfig.ReferenceStorage),
            TypeParameterConstraints = new EquatableArray<string>(GetTypeParameterConstraints(typeSymbol)),
        };
    }

    private static bool HasUsableUnityDebugAssert(Compilation compilation)
    {
        var debugType = compilation.GetTypeByMetadataName(UnityDebugMetadataName);
        return debugType?.GetMembers("Assert")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.IsStatic &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.ReturnsVoid &&
                method.Parameters.Length == 2 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_String) == true;
    }

    /// Re-resolves sentinel values using global config.
    /// Called after Combine in the generator pipeline.
    public static UnionDeclaration ResolveGlobalConfig(UnionDeclaration union, SpireGlobalConfig config)
    {
        var raw = union.RawConfig;
        var referenceStorage = raw.ReferenceStorage == ReferenceStorage.ReadGlobalCfg
            ? config.DefaultReferenceStorage
            : ResolveReferenceStorage(raw.ReferenceStorage);

        var json = raw.Json == -1 // ReadGlobalCfg
            ? config.DefaultJson
            : ResolveJson(raw.Json);

        // ref struct override: force None regardless of global config
        if (union.IsRefStruct && json != JsonLibrary.None)
            json = JsonLibrary.None;

        var jsonDiscriminator = raw.JsonDiscriminator ?? config.DefaultJsonDiscriminator;

        return union with
        {
            ReferenceStorage = referenceStorage,
            Json = json,
            JsonDiscriminator = jsonDiscriminator,
        };
    }

    /// Reads JSON constructor arguments and the named reference-storage policy.
    private static RawAttributeConfig ReadAttributeArgs(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != "DiscriminatedUnionAttribute")
                continue;

            var config = RawAttributeConfig.AllSentinels;
            for (var index = 0; index < attr.ConstructorArguments.Length; index++)
            {
                var argument = attr.ConstructorArguments[index];
                var name = attr.AttributeConstructor?.Parameters[index].Name;
                if (name == "json" && argument.Value is int json)
                    config = config with { Json = json };
                else if (name == "jsonDiscriminator")
                    config = config with { JsonDiscriminator = argument.Value as string };
            }

            foreach (var argument in attr.NamedArguments)
            {
                if (argument.Key == "ReferenceStorage" && argument.Value.Value is int storage)
                    config = config with { ReferenceStorage = (ReferenceStorage)storage };
            }

            return config;
        }

        return RawAttributeConfig.AllSentinels;
    }

    private static ReferenceStorage ResolveReferenceStorage(ReferenceStorage raw) => raw switch
    {
        ReferenceStorage.Typed => ReferenceStorage.Typed,
        ReferenceStorage.Shared => ReferenceStorage.Shared,
        _ => ReferenceStorage.Auto,
    };

    private static JsonLibrary ResolveJson(int raw) => raw switch
    {
        0 => JsonLibrary.None,
        1 => JsonLibrary.SystemTextJson,
        2 => JsonLibrary.NewtonsoftJson,
        3 => JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson,
        _ => JsonLibrary.None, // ReadGlobalCfg(-1) or unknown → default None
    };

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
                Name: current.Name,
                TypeParameters: new EquatableArray<string>(
                    current.TypeParameters.Select(parameter => parameter.Name).ToImmutableArray())));
            current = current.ContainingType;
        }
        chain.Reverse(); // outermost first
        return chain.ToImmutableArray();
    }

    /// Returns "struct" or "record". Returns null for unsupported declaration kinds (class).
    private static string? GetDeclarationKind(TypeDeclarationSyntax syntax)
    {
        return syntax switch
        {
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax record =>
                record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                    ? "struct"
                    : "record",
            _ => null,
        };
    }

    private static INamedTypeSymbol? NearestGenericContainingType(INamedTypeSymbol typeSymbol)
    {
        for (var containing = typeSymbol.ContainingType;
             containing is not null;
             containing = containing.ContainingType)
        {
            if (containing.TypeParameters.Length > 0)
                return containing;
        }

        return null;
    }

    private static ImmutableArray<string> GetTypeParameterConstraints(INamedTypeSymbol typeSymbol)
    {
        var clauses = ImmutableArray.CreateBuilder<string>();
        foreach (var parameter in typeSymbol.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasUnmanagedTypeConstraint)
                constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint)
                constraints.Add("struct");
            else if (parameter.HasReferenceTypeConstraint)
                constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
            else if (parameter.HasNotNullConstraint)
                constraints.Add("notnull");

            constraints.AddRange(parameter.ConstraintTypes.Select(type => DisplayFieldType(type, true)));
            if (parameter.HasConstructorConstraint)
                constraints.Add("new()");
            if (constraints.Count > 0)
                clauses.Add($"where {Emit.IdentifierUtility.Escape(parameter.Name)} : {string.Join(", ", constraints)}");
        }

        return clauses.ToImmutable();
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

    /// Discovers variants from [Variant] static methods (struct path).
    private static ImmutableArray<VariantInfo> DiscoverMethodVariants(
        INamedTypeSymbol typeSymbol,
        bool includeNullableReferenceTypeModifiers,
        INamedTypeSymbol? unitySerializeReferenceAttribute)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && HasVariantAttribute(m))
            .Select(m => ParseMethodVariant(
                m,
                includeNullableReferenceTypeModifiers,
                unitySerializeReferenceAttribute))
            .ToImmutableArray();
    }

    /// Discovers variants from nested types that inherit from the parent (record path).
    private static ImmutableArray<VariantInfo> DiscoverNestedTypeVariants(
        INamedTypeSymbol typeSymbol,
        bool includeNullableReferenceTypeModifiers,
        INamedTypeSymbol? unitySerializeReferenceAttribute)
    {
        return typeSymbol.GetTypeMembers()
            .Where(nested => SymbolEqualityComparer.Default.Equals(
                nested.BaseType?.OriginalDefinition, typeSymbol.OriginalDefinition))
            .Select(nested => ParseNestedTypeVariant(
                nested,
                includeNullableReferenceTypeModifiers,
                unitySerializeReferenceAttribute))
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

    private static VariantInfo ParseMethodVariant(
        IMethodSymbol method,
        bool includeNullableReferenceTypeModifiers,
        INamedTypeSymbol? unitySerializeReferenceAttribute)
    {
        var fields = method.Parameters
            .Select(p => new FieldInfo(
                Name: p.Name,
                TypeFullName: DisplayFieldType(p.Type, includeNullableReferenceTypeModifiers),
                ConstructorTypeKey: FieldNameResolver.ConstructorTypeName(p.Type),
                IsUnmanaged: IsUnmanagedAfterGeneration(p.Type),
                IsReferenceType: p.Type.IsReferenceType,
                JsonName: GetJsonName(p),
                IsUnityUnion: UnitySerializationValidation.IsUnityUnion(p.Type),
                IsUnityUnionCollection: UnitySerializationValidation.IsUnityUnionCollection(p.Type),
                IsUnityComposite: UnitySerializationValidation.IsUnityComposite(p.Type),
                RequiresUnityRuntimeDispatch: UnitySerializationValidation.ContainsTypeParameter(p.Type),
                IsGenericDependent: UnitySerializationValidation.ContainsTypeParameter(p.Type),
                IsUnityManagedReference: HasUnitySerializeReferenceAttribute(
                    p,
                    unitySerializeReferenceAttribute)))
            .ToImmutableArray();

        return new VariantInfo(
            Name: method.Name,
            Fields: new EquatableArray<FieldInfo>(fields),
            JsonName: GetJsonName(method),
            AccessibilityKeyword: AccessibilityToKeyword(method.DeclaredAccessibility),
            FormerlySerializedAs: GetFormerlySerializedAs(method));
    }

    /// Extracts variant info from a nested type's primary constructor parameters.
    private static VariantInfo ParseNestedTypeVariant(
        INamedTypeSymbol nestedType,
        bool includeNullableReferenceTypeModifiers,
        INamedTypeSymbol? unitySerializeReferenceAttribute)
    {
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
                    TypeFullName: DisplayFieldType(p.Type, includeNullableReferenceTypeModifiers),
                    ConstructorTypeKey: FieldNameResolver.ConstructorTypeName(p.Type),
                    IsUnmanaged: IsUnmanagedAfterGeneration(p.Type),
                    IsReferenceType: p.Type.IsReferenceType,
                    JsonName: GetJsonName(p),
                    IsUnityUnion: UnitySerializationValidation.IsUnityUnion(p.Type),
                    IsUnityUnionCollection: UnitySerializationValidation.IsUnityUnionCollection(p.Type),
                    IsUnityComposite: UnitySerializationValidation.IsUnityComposite(p.Type),
                    RequiresUnityRuntimeDispatch: UnitySerializationValidation.ContainsTypeParameter(p.Type),
                    IsGenericDependent: UnitySerializationValidation.ContainsTypeParameter(p.Type),
                    IsUnityManagedReference: HasUnitySerializeReferenceAttribute(
                        p,
                        unitySerializeReferenceAttribute)))
            .ToImmutableArray();

        return new VariantInfo(
            Name: nestedType.Name,
            Fields: new EquatableArray<FieldInfo>(fields),
            JsonName: GetJsonName(nestedType),
            AccessibilityKeyword: ExplicitAccessibility(nestedType),
            FormerlySerializedAs: GetFormerlySerializedAs(nestedType));
    }

    private static string? GetFormerlySerializedAs(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name != "VariantAttribute")
                continue;

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "FormerlySerializedAs" &&
                    argument.Value.Value is string value)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool HasUnitySerializeReferenceAttribute(
        IParameterSymbol parameter,
        INamedTypeSymbol? expectedAttribute)
        => expectedAttribute is not null &&
            parameter.GetAttributes().Any(attribute =>
                attribute.AttributeClass is not null &&
                SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    expectedAttribute));

    private static string DisplayFieldType(
        ITypeSymbol type,
        bool includeNullableReferenceTypeModifiers)
        => type.ToDisplayString(
            includeNullableReferenceTypeModifiers
                ? SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                    SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)
                : SymbolDisplayFormat.FullyQualifiedFormat);

    /// Returns whether a type remains unmanaged after Spire generates any
    /// discriminated-union members. Input symbols can represent generated
    /// unions as empty structs, so ordinary IsUnmanagedType is insufficient
    /// when another generator embeds that union in explicit storage.
    internal static bool IsUnmanagedAfterGeneration(ITypeSymbol type)
        => IsUnmanagedAfterGeneration(
            type,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    private static bool IsUnmanagedAfterGeneration(
        ITypeSymbol type,
        HashSet<ITypeSymbol> visiting)
    {
        if (!type.IsUnmanagedType)
            return false;

        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum ||
            type is not INamedTypeSymbol named)
        {
            return true;
        }

        if (!visiting.Add(type))
            return false;

        try
        {
            // Input symbols do not yet contain the fields emitted for another
            // union. An apparently empty struct can acquire GC references, also
            // changing the classification of ordinary structs containing it.
            if (UnitySerializationValidation.IsUnityUnion(named))
                return false;

            foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic))
            {
                if (!IsUnmanagedAfterGeneration(field.Type, visiting))
                    return false;
            }

            if (named.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "Houtamelo.Spire.DiscriminatedUnionAttribute"))
            {
                foreach (var parameter in named.GetMembers().OfType<IMethodSymbol>()
                             .Where(method => method.IsStatic && HasVariantAttribute(method))
                             .SelectMany(method => method.Parameters))
                {
                    if (!IsUnmanagedAfterGeneration(parameter.Type, visiting))
                        return false;
                }
            }

            return true;
        }
        finally
        {
            visiting.Remove(type);
        }
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
