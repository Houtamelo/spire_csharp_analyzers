using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Parsing;

internal static class UnitySerializationValidation
{
    private const string SerializableAttribute = "System.SerializableAttribute";
    private const string UnityObject = "UnityEngine.Object";
    private const string UnityCallbackReceiver = "UnityEngine.ISerializationCallbackReceiver";
    private const string ListDefinition = "System.Collections.Generic.List<T>";

    public static UnionDiagnostic? Validate(
        INamedTypeSymbol union,
        TypeDeclarationSyntax syntax,
        Compilation compilation,
        ImmutableArray<VariantInfo> variants)
    {
        if (union.TypeKind != TypeKind.Struct ||
            syntax is RecordDeclarationSyntax ||
            union.IsRefLikeType ||
            union.IsReadOnly ||
            syntax.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReadOnlyKeyword)))
        {
            return CreateDiagnostic(
                syntax,
                "SPIRE_DU013",
                "Union declaration cannot use Unity serialization: Unity serialization requires a mutable struct union.");
        }

        if (compilation.GetTypeByMetadataName(UnityCallbackReceiver) is null)
        {
            return CreateDiagnostic(
                syntax,
                "SPIRE_DU016",
                "Unity serialization requires UnityEngine references.");
        }

        var reserved = ValidateReservedMembers(union, syntax, variants);
        if (reserved is not null)
            return reserved;

        foreach (var method in union.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || !HasAttribute(method, "Houtamelo.Spire.VariantAttribute"))
                continue;

            foreach (var parameter in method.Parameters)
            {
                if (parameter.RefKind != RefKind.None)
                {
                    return CreateDiagnostic(
                        syntax,
                        "SPIRE_DU014",
                        $"Variant field '{parameter.Name}' cannot be serialized by Unity.");
                }

                if (!IsSupportedField(parameter.Type, compilation, allowTypeParameter: true, nestedContainer: false))
                {
                    return CreateDiagnostic(
                        syntax,
                        "SPIRE_DU014",
                        $"Variant field '{parameter.Name}' of type '{parameter.Type.ToDisplayString()}' cannot be serialized by Unity.");
                }
            }
        }

        if (union.TypeParameters.Length > 0)
        {
            var closedArgumentError = ValidateClosedGenericUses(union, compilation);
            if (closedArgumentError is not null)
                return CreateDiagnostic(
                    syntax,
                    "SPIRE_DU014",
                    closedArgumentError);
        }

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

    private static bool IsSupportedField(
        ITypeSymbol type,
        Compilation compilation,
        bool allowTypeParameter,
        bool nestedContainer,
        HashSet<INamedTypeSymbol>? compositeVisited = null)
    {
        compositeVisited ??= new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        if (type is ITypeParameterSymbol)
            return allowTypeParameter;

        if (type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer)
            return false;

        if (type is IArrayTypeSymbol array)
        {
            return !nestedContainer && array.Rank == 1 &&
                IsSupportedField(
                    array.ElementType,
                    compilation,
                    allowTypeParameter: true,
                    nestedContainer: true,
                    compositeVisited: compositeVisited) &&
                !IsContainer(array.ElementType);
        }

        if (type is not INamedTypeSymbol named)
            return false;

        if (named.IsTupleType || named.IsAbstract && !IsUnityObject(named, compilation))
            return false;

        if (named.TypeKind == TypeKind.Interface ||
            named.SpecialType == SpecialType.System_Object ||
            named.TypeKind == TypeKind.Dynamic)
        {
            return false;
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return false;

        var metadataName = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (metadataName is "global::System.Decimal" or
            "global::System.DateTime" or
            "global::System.Guid" or
            "global::System.IntPtr" or
            "global::System.UIntPtr" or
            "global::System.Collections.Generic.Dictionary<TKey, TValue>" or
            "global::System.Collections.Generic.IDictionary<TKey, TValue>" ||
            metadataName.StartsWith("global::System.Tuple<", StringComparison.Ordinal) ||
            metadataName.StartsWith("global::System.ValueTuple<", StringComparison.Ordinal))
        {
            return false;
        }

        if (named.TypeKind == TypeKind.Enum)
        {
            var underlying = named.EnumUnderlyingType;
            return underlying is not null && KnownPrimitiveSize(underlying) <= 4;
        }

        if (IsPrimitive(named))
            return true;

        if (IsUnityObject(named, compilation))
            return true;

        if (named.ContainingNamespace?.ToDisplayString() == "UnityEngine")
            return true;

        if (IsList(named))
        {
            return !nestedContainer && named.TypeArguments.Length == 1 &&
                !IsContainer(named.TypeArguments[0]) &&
                IsSupportedField(
                    named.TypeArguments[0],
                    compilation,
                    allowTypeParameter: true,
                    nestedContainer: true,
                    compositeVisited: compositeVisited);
        }

        if (named.IsRefLikeType)
            return false;

        if (!HasSerializableAttribute(named))
            return false;

        if (ImplementsUnityCallback(named, compilation) && IsUnityComposite(named))
            return false;

        if (!compositeVisited.Add(named))
            return false;

        try
        {
            foreach (var argument in named.TypeArguments)
            {
                if (!IsSupportedField(
                        argument,
                        compilation,
                        allowTypeParameter: true,
                        nestedContainer: false,
                        compositeVisited: compositeVisited))
                {
                    return false;
                }
            }

            for (var current = named;
                 current is not null;
                 current = current.BaseType)
            {
                foreach (var field in current.GetMembers().OfType<IFieldSymbol>())
                {
                    if (!IsSerializedCompositeField(field))
                        continue;

                    if (!IsSupportedField(
                            field.Type,
                            compilation,
                            allowTypeParameter: true,
                            nestedContainer,
                            compositeVisited: compositeVisited))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        finally
        {
            compositeVisited.Remove(named);
        }
    }

    private static bool IsContainer(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        return type is INamedTypeSymbol named &&
            (IsList(named) ||
             named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
             named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>");
    }

    private static bool IsPrimitive(INamedTypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Decimal)
            return false;

        if (type.SpecialType is SpecialType.System_String)
            return true;

        if (type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_Double)
            return true;

        return false;
    }

    private static int KnownPrimitiveSize(ITypeSymbol type)
        => type.SpecialType switch
        {
            SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Boolean => 1,
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Char => 2,
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
            _ => 8,
        };

    private static bool IsList(INamedTypeSymbol type)
        => type.OriginalDefinition.ToDisplayString() == ListDefinition;

    private static bool IsUnityObject(INamedTypeSymbol type, Compilation compilation)
    {
        var unityObject = compilation.GetTypeByMetadataName(UnityObject);
        if (unityObject is null)
            return false;

        return SymbolEqualityComparer.Default.Equals(type, unityObject) ||
            type.InheritsFrom(unityObject);
    }

    private static bool ImplementsUnityCallback(
        INamedTypeSymbol type,
        Compilation compilation)
    {
        var callbackReceiver = compilation.GetTypeByMetadataName(UnityCallbackReceiver);
        return callbackReceiver is not null &&
            type.AllInterfaces.Any(interfaceType =>
                SymbolEqualityComparer.Default.Equals(interfaceType, callbackReceiver));
    }

    private static string? ValidateClosedGenericUses(
        INamedTypeSymbol union,
        Compilation compilation)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in EnumerateNamedTypes(compilation.GlobalNamespace))
        {
            foreach (var member in type.GetMembers())
            {
                switch (member)
                {
                    case IFieldSymbol field:
                        {
                            var failure = InspectConstructedType(field.Type, union, compilation, seen);
                            if (failure is not null) return failure;
                            break;
                        }
                    case IPropertySymbol property:
                        {
                            var failure = InspectConstructedType(property.Type, union, compilation, seen);
                            if (failure is not null) return failure;
                            break;
                        }
                    case IMethodSymbol method:
                        {
                            var failure = InspectConstructedType(method.ReturnType, union, compilation, seen);
                            if (failure is not null) return failure;
                            foreach (var parameter in method.Parameters)
                            {
                                failure = InspectConstructedType(parameter.Type, union, compilation, seen);
                                if (failure is not null) return failure;
                            }
                            break;
                        }
                }
            }
        }

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeSyntax in syntaxTree.GetRoot().DescendantNodes().OfType<TypeSyntax>())
            {
                var failure = InspectConstructedType(
                    semanticModel.GetTypeInfo(typeSyntax).Type,
                    union,
                    compilation,
                    seen);
                if (failure is not null)
                    return failure;
            }
        }

        return null;
    }

    private static string? InspectConstructedType(
        ITypeSymbol? type,
        INamedTypeSymbol union,
        Compilation compilation,
        HashSet<string> seen)
    {
        if (type is null)
            return null;

        if (type is IArrayTypeSymbol array)
            return InspectConstructedType(array.ElementType, union, compilation, seen);

        if (type is not INamedTypeSymbol named)
            return null;

        if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, union.OriginalDefinition) &&
            named.TypeArguments.Any(argument => argument.TypeKind != TypeKind.TypeParameter))
        {
            var key = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (seen.Add(key))
            {
                foreach (var argument in named.TypeArguments)
                {
                    if (!IsSupportedField(argument, compilation, allowTypeParameter: false, nestedContainer: false))
                        return $"Closed generic Unity union argument '{argument.ToDisplayString()}' cannot be serialized by Unity.";
                }
            }
        }

        if (named.IsGenericType && HasSourceLocation(named.OriginalDefinition))
        {
            var memberKey = "members:" + named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (seen.Add(memberKey))
            {
                foreach (var member in named.GetMembers())
                {
                    var failure = member switch
                    {
                        IFieldSymbol field => InspectConstructedType(field.Type, union, compilation, seen),
                        IPropertySymbol property => InspectConstructedType(property.Type, union, compilation, seen),
                        IMethodSymbol method => InspectMethodConstructedTypes(method, union, compilation, seen),
                        _ => null,
                    };

                    if (failure is not null)
                        return failure;
                }
            }
        }

        foreach (var argument in named.TypeArguments)
        {
            var failure = InspectConstructedType(argument, union, compilation, seen);
            if (failure is not null) return failure;
        }

        return null;
    }

    private static string? InspectMethodConstructedTypes(
        IMethodSymbol method,
        INamedTypeSymbol union,
        Compilation compilation,
        HashSet<string> seen)
    {
        var failure = InspectConstructedType(method.ReturnType, union, compilation, seen);
        if (failure is not null)
            return failure;

        foreach (var parameter in method.Parameters)
        {
            failure = InspectConstructedType(parameter.Type, union, compilation, seen);
            if (failure is not null)
                return failure;
        }

        return null;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers().Where(type => HasSourceLocation(type)))
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
                yield return nested;
        }

        foreach (var child in root.GetNamespaceMembers().Where(namespaceSymbol => HasSourceLocation(namespaceSymbol)))
        {
            foreach (var type in EnumerateNamedTypes(child))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol root)
    {
        foreach (var type in root.GetTypeMembers().Where(type => HasSourceLocation(type)))
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
                yield return nested;
        }
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal));

    private static bool HasSourceLocation(ISymbol symbol)
        => symbol.Locations.Any(location => location.IsInSource);

    private static UnionDiagnostic CreateDiagnostic(
        TypeDeclarationSyntax syntax,
        string id,
        string message)
    {
        var location = syntax.Identifier.GetLocation();
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

    private static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return false;
    }

}
