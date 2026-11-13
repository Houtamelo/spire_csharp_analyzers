using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

internal enum NullableKind
{
    ValueType,
    NullableValueType,
    RefType,
    NullableRefType,
}

/// Detects whether a type symbol is a discriminated union and extracts variant info.
internal sealed class UnionTypeInfo
{
    public bool IsStructUnion { get; }
    public bool IsRecordUnion => !IsStructUnion;
    public NullableKind Nullability { get; }
    public ImmutableArray<string> VariantNames { get; }
    /// For record unions: the variant type symbols (for type pattern matching).
    public ImmutableArray<INamedTypeSymbol> VariantTypes { get; }
    /// For struct unions: the nested Kind enum type (for constant pattern matching).
    public INamedTypeSymbol? KindEnumType { get; }

    /// Returns the positional field symbols for a struct-union variant. Method
    /// variants use their attributed factory parameters; authored record
    /// variants use the synthesized positional Deconstruct parameters. Keeping
    /// this lookup here prevents consumer analyzers from assuming every variant
    /// has a static factory method.
    public static ImmutableArray<IParameterSymbol> GetVariantFields(
        INamedTypeSymbol unionType,
        string variantName)
    {
        var factory = unionType.GetMembers(variantName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.IsStatic &&
                method.MethodKind == MethodKind.Ordinary &&
                HasVariantAttribute(method));
        if (factory is not null)
            return factory.Parameters;

        var variantType = unionType.GetTypeMembers(variantName)
            .FirstOrDefault(type => HasVariantAttribute(type));
        if (variantType is null)
            return ImmutableArray<IParameterSymbol>.Empty;

        // The union schema is the record declaration's positional parameter
        // list. Do not infer it from authored constructors or Deconstruct
        // overloads: those members may describe additional, unrelated APIs.
        var declarations = variantType.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .ToArray();
        // A partial record can have a parameterless/generated declaration
        // before the user declaration that carries the primary constructor.
        // Prefer the part with actual positional parameters, then preserve an
        // explicit empty parameter list as the fieldless declaration.
        var declaration = declarations.FirstOrDefault(candidate =>
                candidate.ParameterList?.Parameters.Count > 0)
            ?? declarations.FirstOrDefault(candidate => candidate.ParameterList is not null);
        var positionalParameters = declaration?.ParameterList?.Parameters;
        if (positionalParameters is null || positionalParameters.Value.Count == 0)
            return ImmutableArray<IParameterSymbol>.Empty;

        var parameterNames = positionalParameters.Value
            .Select(parameter => parameter.Identifier.ValueText)
            .ToArray();
        var constructors = variantType.InstanceConstructors
            .Where(constructor => constructor.Parameters.Length == parameterNames.Length)
            .Where(constructor => constructor.Parameters
                .Select(parameter => parameter.Name)
                .SequenceEqual(parameterNames, System.StringComparer.Ordinal))
            .OrderByDescending(constructor => constructor.IsImplicitlyDeclared)
            .ToArray();
        return constructors.Length == 0
            ? ImmutableArray<IParameterSymbol>.Empty
            : constructors[0].Parameters;
    }

    public static bool IsRecordVariant(INamedTypeSymbol unionType, string variantName)
        => unionType.GetTypeMembers(variantName)
            .Any(type => HasVariantAttribute(type));

    public static INamedTypeSymbol? GetRecordVariantType(
        INamedTypeSymbol unionType,
        string variantName)
        => unionType.GetTypeMembers(variantName)
            .FirstOrDefault(type => HasVariantAttribute(type));

    private UnionTypeInfo(
        bool isStruct,
        NullableKind nullability,
        ImmutableArray<string> names,
        ImmutableArray<INamedTypeSymbol> types,
        INamedTypeSymbol? kindEnumType = null)
    {
        IsStructUnion = isStruct;
        Nullability = nullability;
        VariantNames = names;
        VariantTypes = types;
        KindEnumType = kindEnumType;
    }

    /// Returns a UnionTypeInfo if the type has [DiscriminatedUnion] and valid variant structure, null otherwise.
    /// Handles Nullable&lt;T&gt; unwrapping for struct unions internally.
    public static UnionTypeInfo? TryCreate(ITypeSymbol type, INamedTypeSymbol duAttributeType)
    {
        // Unwrap Nullable<T> for value types
        bool isNullableValueType = type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1;

        if (isNullableValueType)
            type = ((INamedTypeSymbol)type).TypeArguments[0];

        if (!HasAttribute(type, duAttributeType))
            return null;

        NullableKind nullability;
        if (type.IsValueType)
            nullability = isNullableValueType ? NullableKind.NullableValueType : NullableKind.ValueType;
        else
            nullability = type.NullableAnnotation != NullableAnnotation.NotAnnotated
                ? NullableKind.NullableRefType
                : NullableKind.RefType;

        if (type.IsValueType)
            return TryCreateStruct(type, nullability);
        else
            return TryCreateRecord(type, nullability);
    }

    private static UnionTypeInfo? TryCreateStruct(ITypeSymbol type, NullableKind nullability)
    {
        // Struct unions have a nested "Kind" enum with one field per variant
        var kindEnum = type.GetTypeMembers("Kind")
            .FirstOrDefault(t => t.TypeKind == TypeKind.Enum);

        if (kindEnum is null)
            return null;

        var names = kindEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name)
            .ToImmutableArray();

        return new UnionTypeInfo(true, nullability, names, ImmutableArray<INamedTypeSymbol>.Empty, kindEnum);
    }

    private static UnionTypeInfo? TryCreateRecord(ITypeSymbol type, NullableKind nullability)
    {
        // Record unions have sealed nested types that inherit from the union
        var variants = type.GetTypeMembers()
            .Where(nested => nested.IsSealed &&
                SymbolEqualityComparer.Default.Equals(
                    nested.BaseType?.OriginalDefinition,
                    type.OriginalDefinition))
            .ToImmutableArray();

        if (variants.Length == 0)
            return null;

        var names = variants.Select(v => v.Name).ToImmutableArray();
        return new UnionTypeInfo(false, nullability, names, variants);
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        return type.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
    }

    private static bool HasVariantAttribute(ISymbol symbol)
        => symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "VariantAttribute");
}
