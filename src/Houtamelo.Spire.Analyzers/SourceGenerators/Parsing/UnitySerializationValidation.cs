using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Parsing;

internal static class UnitySerializationValidation
{
    private const string SerializableAttribute = "System.SerializableAttribute";
    private const string UnityCallbackReceiver = "UnityEngine.ISerializationCallbackReceiver";
    private const string ListDefinition = "System.Collections.Generic.List<T>";

    public static UnionDiagnostic? Validate(
        INamedTypeSymbol union,
        TypeDeclarationSyntax syntax,
        Compilation compilation,
        ImmutableArray<VariantInfo> variants,
        out ImmutableArray<string> invalidAliasVariants)
    {
        invalidAliasVariants = ImmutableArray<string>.Empty;

        if (union.TypeKind != TypeKind.Struct ||
            syntax is RecordDeclarationSyntax ||
            union.IsRefLikeType ||
            union.IsReadOnly ||
            syntax.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReadOnlyKeyword)))
        {
            // Alias payloads are emitted by the core payload representation as
            // well, so still mark malformed aliases even when Unity callbacks
            // are unavailable for the declaration shape.
            _ = ValidateAliases(union, syntax, variants, out invalidAliasVariants);
            return CreateDiagnostic(
                syntax,
                "SPIRE_DU013",
                "Union declaration cannot use Unity serialization: Unity serialization requires a mutable struct union.");
        }

        if (compilation.GetTypeByMetadataName(UnityCallbackReceiver) is null)
        {
            _ = ValidateAliases(union, syntax, variants, out invalidAliasVariants);
            return CreateDiagnostic(
                syntax,
                "SPIRE_DU016",
                "Unity serialization requires UnityEngine references.");
        }

        var reserved = ValidateReservedMembers(union, syntax, variants);
        if (reserved is not null)
        {
            _ = ValidateAliases(union, syntax, variants, out invalidAliasVariants);
            return reserved;
        }

        var aliases = ValidateAliases(
            union,
            syntax,
            variants,
            out invalidAliasVariants);
        if (aliases is not null)
            return aliases;

        return null;
    }

    public static bool HasSerializableAttribute(INamedTypeSymbol type)
        => HasAttribute(type, SerializableAttribute);

    public static bool IsUnityUnion(ITypeSymbol type)
        => type is INamedTypeSymbol named &&
            HasAttribute(named, "Houtamelo.Spire.DiscriminatedUnionAttribute") &&
            HasSerializableAttribute(named);

    public static bool IsUnityUnionCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return IsUnityUnion(array.ElementType);

        return type is INamedTypeSymbol named &&
            IsList(named) &&
            named.TypeArguments.Length == 1 &&
            IsUnityUnion(named.TypeArguments[0]);
    }

    public static bool IsUnityComposite(ITypeSymbol type)
        => IsUnityComposite(type, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

    public static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol)
            return true;

        if (type is IArrayTypeSymbol array)
            return ContainsTypeParameter(array.ElementType);

        return type is INamedTypeSymbol named &&
            (named.TypeArguments.Any(ContainsTypeParameter) ||
             named.ContainingType is not null && ContainsTypeParameter(named.ContainingType));
    }

    private static bool IsUnityComposite(
        ITypeSymbol type,
        HashSet<INamedTypeSymbol> visited)
    {
        if (IsUnityUnion(type))
            return false;

        if (type is IArrayTypeSymbol array)
            return IsUnityComposite(array.ElementType, visited);

        if (type is not INamedTypeSymbol named ||
            IsList(named) && named.TypeArguments.Length != 1 ||
            !HasSerializableAttribute(named))
        {
            return false;
        }

        if (IsList(named))
            return IsUnityComposite(named.TypeArguments[0], visited);

        if (!visited.Add(named))
            return false;

        for (var current = named;
             current is not null;
             current = current.BaseType)
        {
            foreach (var field in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (!IsSerializedCompositeField(field))
                    continue;

                if (IsUnityUnion(field.Type) ||
                    IsUnityUnionCollection(field.Type) ||
                    IsUnityComposite(field.Type, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSerializedCompositeField(IFieldSymbol field)
    {
        if (field.IsStatic || field.IsConst || field.IsReadOnly ||
            HasAttribute(field, "System.NonSerializedAttribute"))
        {
            return false;
        }

        return field.DeclaredAccessibility == Accessibility.Public ||
            HasAttribute(field, "UnityEngine.SerializeField");
    }

    private static UnionDiagnostic? ValidateReservedMembers(
        INamedTypeSymbol union,
        TypeDeclarationSyntax syntax,
        ImmutableArray<VariantInfo> variants)
    {
        foreach (var variant in variants)
        {
            if (variant.Name.StartsWith("__Spire", StringComparison.Ordinal) ||
                variant.Name == "_kind" ||
                variant.Name == "_serializedPayload" ||
                variant.Name == "OnBeforeSerialize" ||
                variant.Name == "OnAfterDeserialize")
            {
                return CreateDiagnostic(
                    syntax,
                    "SPIRE_DU015",
                    $"Member '{variant.Name}' conflicts with generated Unity serialization.");
            }

            foreach (var field in variant.Fields)
            {
                if (field.Name.StartsWith("__Spire", StringComparison.Ordinal) ||
                    field.Name == "_kind" ||
                    field.Name == "_serializedPayload" ||
                    field.Name == "OnBeforeSerialize" ||
                    field.Name == "OnAfterDeserialize")
                {
                    return CreateDiagnostic(
                        syntax,
                        "SPIRE_DU015",
                        $"Member '{field.Name}' conflicts with generated Unity serialization.");
                }
            }
        }

        foreach (var member in union.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;

            if (member.Name.StartsWith("__Spire", StringComparison.Ordinal) ||
                member.Name == "_kind" ||
                member.Name == "_serializedPayload" ||
                member.Name == "OnBeforeSerialize" ||
                member.Name == "OnAfterDeserialize")
            {
                return CreateDiagnostic(
                    syntax,
                    "SPIRE_DU015",
                    $"Member '{member.Name}' conflicts with generated Unity serialization.");
            }
        }

        return null;
    }

    private static UnionDiagnostic? ValidateAliases(
        INamedTypeSymbol union,
        TypeDeclarationSyntax syntax,
        ImmutableArray<VariantInfo> variants,
        out ImmutableArray<string> invalidAliasVariants)
    {
        var currentNames = new HashSet<string>(
            variants.Select(variant => variant.Name),
            StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var payloadNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var invalid = ImmutableArray.CreateBuilder<string>();
        UnionDiagnostic? firstDiagnostic = null;

        void MarkInvalid(VariantInfo variant, string message)
        {
            invalid.Add(variant.Name);
            firstDiagnostic ??= CreateAliasDiagnostic(
                union,
                syntax,
                variant.Name,
                message);
        }

        foreach (var variant in variants)
        {
            var alias = variant.FormerlySerializedAs;
            if (string.IsNullOrEmpty(alias) ||
                string.Equals(alias, variant.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var aliasValue = alias!;
            if (!IsValidAlias(aliasValue))
            {
                MarkInvalid(
                    variant,
                    $"FormerlySerializedAs alias '{aliasValue}' is not a valid variant name.");
                continue;
            }

            if (currentNames.Contains(aliasValue))
            {
                MarkInvalid(
                    variant,
                    $"FormerlySerializedAs alias '{aliasValue}' identifies another current variant.");
                continue;
            }

            if (aliases.TryGetValue(aliasValue, out var priorVariant))
            {
                MarkInvalid(
                    variant,
                    $"FormerlySerializedAs alias '{aliasValue}' is already used by variant '{priorVariant}'.");
                continue;
            }

            var payloadName = SerializedPayloadIdentity(aliasValue);
            if (payloadNames.TryGetValue(payloadName, out priorVariant))
            {
                MarkInvalid(
                    variant,
                    $"FormerlySerializedAs alias '{aliasValue}' maps to the same generated payload name as '{priorVariant}'.");
                continue;
            }

            var collidesWithCurrent = false;
            foreach (var currentName in currentNames)
            {
                if (string.Equals(
                        payloadName,
                        SerializedPayloadIdentity(currentName),
                        StringComparison.Ordinal) &&
                    !string.Equals(aliasValue, currentName, StringComparison.Ordinal))
                {
                    MarkInvalid(
                        variant,
                        $"FormerlySerializedAs alias '{aliasValue}' collides with generated payload name '{currentName}'.");
                    collidesWithCurrent = true;
                    break;
                }
            }

            if (collidesWithCurrent)
                continue;

            aliases.Add(aliasValue, variant.Name);
            payloadNames.Add(payloadName, variant.Name);
        }

        invalidAliasVariants = invalid.ToImmutable();
        return firstDiagnostic;
    }

    private static bool IsValidAlias(string alias)
        => SyntaxFacts.IsValidIdentifier(alias) ||
            SyntaxFacts.GetKeywordKind(alias) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(alias) != SyntaxKind.None;

    private static string SerializedPayloadIdentity(string name)
    {
        var raw = name.StartsWith("@", StringComparison.Ordinal)
            ? name.Substring(1)
            : name;
        var chars = raw
            .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();
        return chars.Length == 0 ? "Value" : new string(chars);
    }

    private static UnionDiagnostic CreateAliasDiagnostic(
        INamedTypeSymbol union,
        TypeDeclarationSyntax syntax,
        string variantName,
        string message)
    {
        var location = FindAliasLocation(union, variantName) ?? syntax.Identifier.GetLocation();
        return CreateDiagnostic(location, "SPIRE_DU017", message);
    }

    private static Location? FindAliasLocation(INamedTypeSymbol union, string variantName)
    {
        var symbol = union.GetMembers()
            .FirstOrDefault(member => member.Name == variantName &&
                (member is IMethodSymbol || member is INamedTypeSymbol));
        if (symbol is null)
            return null;

        var attribute = symbol.GetAttributes()
            .FirstOrDefault(candidate => candidate.AttributeClass?.Name == "VariantAttribute" &&
                candidate.NamedArguments.Any(argument =>
                    argument.Key == "FormerlySerializedAs"));
        var declaration = symbol.DeclaringSyntaxReferences
            .FirstOrDefault()
            ?.GetSyntax() as MethodDeclarationSyntax;
        var syntaxAttribute = declaration?.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(candidate =>
                candidate.Name.ToString().EndsWith("Variant", StringComparison.Ordinal));
        var aliasArgument = syntaxAttribute?.ArgumentList?.Arguments
            .FirstOrDefault(argument =>
                argument.NameEquals?.Name.Identifier.ValueText == "FormerlySerializedAs");
        return aliasArgument?.GetLocation() ?? syntaxAttribute?.GetLocation() ??
            attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
    }

    private static bool IsList(INamedTypeSymbol type)
        => type.OriginalDefinition.ToDisplayString() == ListDefinition;

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal));

    private static UnionDiagnostic CreateDiagnostic(
        TypeDeclarationSyntax syntax,
        string id,
        string message)
        => CreateDiagnostic(syntax.Identifier.GetLocation(), id, message);

    private static UnionDiagnostic CreateDiagnostic(
        Location location,
        string id,
        string message)
    {
        var lineSpan = location.GetLineSpan();
        return new UnionDiagnostic(
            id,
            message,
            IsError: true,
            lineSpan.Path ?? string.Empty,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character,
            lineSpan.EndLinePosition.Line,
            lineSpan.EndLinePosition.Character);
    }

}
