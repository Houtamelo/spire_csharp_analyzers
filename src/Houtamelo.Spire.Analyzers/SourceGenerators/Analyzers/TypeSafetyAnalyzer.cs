using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
            AnalyzerDescriptors.SPIRE012_FieldCountMismatch);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var duAttr = compilationCtx.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
            if (duAttr is null) return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeRecursivePattern(ctx, duAttr),
                OperationKind.RecursivePattern);
        });
    }

    private static void AnalyzeRecursivePattern(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var recursive = (IRecursivePatternOperation)ctx.Operation;

        // Variant payloads are ordinary generated structs. Their nested
        // property/positional patterns must be checked against the payload
        // fields and Deconstruct parameters, while the enclosing union pattern
        // remains responsible for selecting the accessor.
        if (AnalyzeGeneratedPayloadPattern(ctx, recursive, duAttr))
            return;

        // Property pattern path: { kind: Shape.Kind.X, field: type name }
        // Check PropertySubpatterns first — property patterns take priority even when
        // a Deconstruct method exists on the type.
        if (!recursive.PropertySubpatterns.IsEmpty)
        {
            AnalyzePropertyPattern(ctx, recursive, duAttr);
            return;
        }

        // Deconstruct pattern path: (Kind.X, type name)
        if (recursive.DeconstructSymbol is not IMethodSymbol method)
            return;

        var containingType = method.ContainingType;
        if (containingType is null)
            return;

        // Only analyze struct unions with [DiscriminatedUnion]
        var unionInfo = UnionTypeInfo.TryCreate(containingType, duAttr);
        if (unionInfo is null || !unionInfo.IsStructUnion)
            return;

        var subpatterns = recursive.DeconstructionSubpatterns;
        var parameters = method.Parameters;

        // Field count check — Roslyn usually catches this at compile time,
        // but report it if it somehow gets through
        if (subpatterns.Length != parameters.Length)
        {
            var variantName = ResolveVariantName(subpatterns, unionInfo);
            var expectedFieldCount = parameters.Length - 1;
            var actualFieldCount = subpatterns.Length - 1;

            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE012_FieldCountMismatch,
                recursive.Syntax.GetLocation(),
                variantName ?? containingType.Name,
                expectedFieldCount,
                actualFieldCount));
            return;
        }

        // Resolve variant name and its actual field types from the factory method
        var resolvedVariant = ResolveVariantName(subpatterns, unionInfo);
        var factoryParams = resolvedVariant is not null
            ? GetVariantFieldTypes(containingType, resolvedVariant)
            : null;

        // Type check each field subpattern (skip index 0, which is the Kind param)
        for (int i = 1; i < subpatterns.Length; i++)
        {
            var subpattern = subpatterns[i];
            var deconstructParamType = parameters[i].Type;

            // Determine the expected type: use the factory method's param type if available,
            // fall back to the Deconstruct param type.
            // This handles the case where Deconstruct uses `object?` for boxed single-field variants.
            var fieldIndex = i - 1; // 0-based field index (excluding Kind)
            var expectedType = factoryParams is not null && fieldIndex < factoryParams.Value.Length
                ? factoryParams.Value[fieldIndex]
                : deconstructParamType;

            ITypeSymbol? actualType = null;

            switch (subpattern)
            {
                case IDiscardPatternOperation:
                    // Untyped discard — always OK
                    continue;

                case IDeclarationPatternOperation declPattern:
                    // `var x` — infers type from context, always OK
                    if (declPattern.InputType is not null &&
                        SymbolEqualityComparer.Default.Equals(
                            declPattern.InputType, declPattern.MatchedType))
                        continue;

                    // `object? val` — boxing is always valid for any field type
                    if (IsObjectOrNullableObject(declPattern.MatchedType))
                        continue;

                    actualType = declPattern.MatchedType;
                    break;

                case IConstantPatternOperation constPattern:
                    actualType = constPattern.Value?.Type;
                    break;

                default:
                    continue;
            }

            if (actualType is null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(actualType, expectedType))
                continue;

            // Also allow matching against the Deconstruct param type directly
            // (e.g., `object? val` against `object?` param)
            if (SymbolEqualityComparer.Default.Equals(actualType, deconstructParamType))
                continue;

            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("ExpectedType", expectedType.ToDisplayString());
            properties.Add("FieldIndex", fieldIndex.ToString());

            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
                subpattern.Syntax.GetLocation(),
                properties.ToImmutable(),
                resolvedVariant ?? containingType.Name,
                fieldIndex,
                expectedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                actualType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool AnalyzeGeneratedPayloadPattern(
        OperationAnalysisContext ctx,
        IRecursivePatternOperation recursive,
        INamedTypeSymbol duAttr)
    {
        if (!TryGetPayloadOwner(
                recursive,
                duAttr,
                out var unionType,
                out var unionInfo,
                out var accessor))
        {
            return false;
        }

        var payloadType = accessor.Type as INamedTypeSymbol;
        if (payloadType is null || !payloadType.IsValueType)
            return false;

        var variants = VariantFieldMap.TryCreate(unionType, unionInfo!)
            ?.GetOwningVariants(accessor.Name);
        var variantName = variants is { Count: > 0 }
            ? variants.First()
            : unionType.Name;

        if (!recursive.PropertySubpatterns.IsEmpty)
        {
            foreach (var propertySubpattern in recursive.PropertySubpatterns)
            {
                if (propertySubpattern.Member is not IPropertyReferenceOperation &&
                    propertySubpattern.Member is not IFieldReferenceOperation)
                {
                    continue;
                }

                var memberName = propertySubpattern.Member switch
                {
                    IPropertyReferenceOperation property => property.Property.Name,
                    IFieldReferenceOperation field => field.Field.Name,
                    _ => string.Empty,
                };
                var normalizedMemberName = memberName.TrimStart('@');
                var memberType = payloadType.GetMembers(memberName)
                    .Concat(payloadType.GetMembers(normalizedMemberName))
                    .OfType<IFieldSymbol>()
                    .Select(field => field.Type)
                    .Concat(payloadType.GetMembers(memberName)
                        .OfType<IPropertySymbol>()
                        .Select(property => property.Type))
                    .FirstOrDefault();
                if (memberType is null)
                    continue;

                ReportPayloadPatternType(
                    ctx,
                    propertySubpattern.Pattern,
                    memberType,
                    variantName,
                    GetPayloadFieldIndex(payloadType, memberName),
                    expectedAsVar: true);
            }

            return true;
        }

        if (recursive.DeconstructSymbol is not IMethodSymbol deconstruct)
            return true;

        var subpatterns = recursive.DeconstructionSubpatterns;
        for (var index = 0; index < subpatterns.Length && index < deconstruct.Parameters.Length; index++)
        {
            ReportPayloadPatternType(
                ctx,
                subpatterns[index],
                deconstruct.Parameters[index].Type,
                variantName,
                index,
                expectedAsVar: false);
        }

        return true;
    }

    private static void ReportPayloadPatternType(
        OperationAnalysisContext ctx,
        IPatternOperation pattern,
        ITypeSymbol expectedType,
        string variantName,
        int fieldIndex,
        bool expectedAsVar)
    {
        if (pattern is not IDeclarationPatternOperation &&
            pattern is not IDiscardPatternOperation)
            return;

        var actualType = pattern is IDeclarationPatternOperation declaration
            ? declaration.MatchedType
            : null;
        var actualTypeDisplay = actualType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (actualType is not null)
        {
            if (IsObjectOrNullableObject(actualType))
            {
                return;
            }

            if (TypesCompatibleForPattern(actualType, expectedType))
            {
                // Roslyn can bind an invalid typed discard to the input type
                // while still retaining the explicitly written type in syntax.
                // Inspect that spelling so `string _` against an int field is
                // still offered the normal type fix.
                if (pattern.Syntax is not DeclarationPatternSyntax declarationSyntax ||
                    TypeNamesMatch(declarationSyntax.Type.ToString(), expectedType))
                {
                    return;
                }

                actualTypeDisplay = declarationSyntax.Type.ToString();
            }
        }
        else if (pattern.Syntax is DeclarationPatternSyntax declarationSyntax)
        {
            actualTypeDisplay = declarationSyntax.Type.ToString();
            if (IsObjectTypeName(actualTypeDisplay) ||
                TypeNamesMatch(actualTypeDisplay, expectedType))
            {
                return;
            }
        }
        else
        {
            return;
        }

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        var expectedDisplay = expectedAsVar
            ? "var"
            : expectedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        properties.Add("ExpectedType", expectedDisplay);
        properties.Add("FieldIndex", fieldIndex.ToString());
        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
            pattern.Syntax.GetLocation(),
            properties.ToImmutable(),
            variantName,
            fieldIndex,
            expectedDisplay,
            actualTypeDisplay ?? "?"));
    }

    private static bool IsObjectTypeName(string typeName)
        => string.Equals(typeName.Trim(), "object", System.StringComparison.Ordinal) ||
            string.Equals(typeName.Trim(), "System.Object", System.StringComparison.Ordinal);

    private static bool TypeNamesMatch(string syntaxTypeName, ITypeSymbol expectedType)
    {
        var syntaxName = syntaxTypeName.Trim();
        if (syntaxName.StartsWith("global::", System.StringComparison.Ordinal))
            syntaxName = syntaxName.Substring("global::".Length);
        var expectedName = expectedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return string.Equals(syntaxName, expectedName, System.StringComparison.Ordinal) ||
            string.Equals(
                syntaxName.TrimEnd('?'),
                expectedName.TrimEnd('?'),
                System.StringComparison.Ordinal);
    }

    private static bool TypesCompatibleForPattern(ITypeSymbol actual, ITypeSymbol expected)
    {
        if (SymbolEqualityComparer.Default.Equals(actual, expected))
            return true;

        // Nullable reference annotations are filters, not storage type changes.
        if (actual.IsReferenceType && expected.IsReferenceType &&
            SymbolEqualityComparer.Default.Equals(
                actual.WithNullableAnnotation(NullableAnnotation.None),
                expected.WithNullableAnnotation(NullableAnnotation.None)))
        {
            return true;
        }

        return false;
    }

    private static int GetPayloadFieldIndex(INamedTypeSymbol payloadType, string memberName)
    {
        var properties = payloadType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.GetMethod is not null &&
                property.SetMethod is null)
            .ToArray();
        for (var index = 0; index < properties.Length; index++)
        {
            if (string.Equals(properties[index].Name, memberName.TrimStart('@'), System.StringComparison.Ordinal))
                return index;
        }

        return 0;
    }

    private static bool TryGetPayloadOwner(
        IRecursivePatternOperation recursive,
        INamedTypeSymbol duAttr,
        out INamedTypeSymbol unionType,
        out UnionTypeInfo? unionInfo,
        out IPropertySymbol accessor)
    {
        unionType = null!;
        unionInfo = null;
        accessor = null!;
        var current = recursive.Parent;
        while (current is not null)
        {
            if (current is IPropertySubpatternOperation propertySubpattern &&
                propertySubpattern.Member is IPropertyReferenceOperation propertyReference &&
                propertyReference.Property.ContainingType is INamedTypeSymbol candidate)
            {
                var candidateInfo = UnionTypeInfo.TryCreate(candidate, duAttr);
                var isAccessor = candidateInfo is { IsStructUnion: true } &&
                    VariantFieldMap.TryCreate(candidate, candidateInfo)
                        ?.GetOwningVariants(propertyReference.Property.Name)
                        .IsEmpty == false;
                var accessorType = propertyReference.Property.Type;
                var patternInputType = recursive.MatchedType ?? recursive.InputType;
                if (isAccessor &&
                    SymbolEqualityComparer.Default.Equals(
                        accessorType,
                        patternInputType))
                {
                    unionType = candidate;
                    unionInfo = candidateInfo;
                    accessor = propertyReference.Property;
                    return true;
                }
            }

            if (current is IMethodBodyOperation or IAnonymousFunctionOperation)
                break;
            current = current.Parent;
        }

        return false;
    }

    /// Returns true if the type is `object` or `object?`.
    private static bool IsObjectOrNullableObject(ITypeSymbol? type)
    {
        if (type is null)
            return false;
        return type.SpecialType == SpecialType.System_Object;
    }

    /// Resolves the variant name from the first subpattern (Kind constant).
    private static string? ResolveVariantName(
        ImmutableArray<IPatternOperation> subpatterns, UnionTypeInfo unionInfo)
    {
        if (subpatterns.IsEmpty)
            return null;

        if (subpatterns[0] is not IConstantPatternOperation constant)
            return null;

        var valueOp = constant.Value;
        if (valueOp is null || valueOp.Type is null)
            return null;

        var kindEnum = unionInfo.KindEnumType;
        if (kindEnum is null)
            return null;

        if (!SymbolEqualityComparer.Default.Equals(valueOp.Type, kindEnum))
            return null;

        if (!valueOp.ConstantValue.HasValue)
            return null;

        var value = valueOp.ConstantValue.Value;
        if (TryGetOrdinal(value, out int ordinal) && ordinal >= 0 && ordinal < unionInfo.VariantNames.Length)
            return unionInfo.VariantNames[ordinal];

        return null;
    }

    private static bool TryGetOrdinal(object? value, out int ordinal)
    {
        switch (value)
        {
            case byte b: ordinal = b; return true;
            case ushort u: ordinal = u; return true;
            case int i: ordinal = i; return true;
            case uint ui: ordinal = (int)ui; return true;
            default: ordinal = 0; return false;
        }
    }

    /// Gets the actual field types for a variant from its factory method.
    /// Returns the parameter types of the static factory method matching the variant name.
    private static ImmutableArray<ITypeSymbol>? GetVariantFieldTypes(
        INamedTypeSymbol unionType, string variantName)
    {
        // Look for the static factory method (e.g., `public static partial Shape Circle(double radius)`)
        var factoryMethod = unionType.GetMembers(variantName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.MethodKind == MethodKind.Ordinary);

        if (factoryMethod is null)
            return null;

        return factoryMethod.Parameters
            .Select(p => p.Type)
            .ToImmutableArray();
    }

    /// Analyzes a property pattern (e.g., { kind: Shape.Kind.Circle, radius: string bad })
    /// for type mismatches. For property patterns, the expected type is always "var".
    private static void AnalyzePropertyPattern(
        OperationAnalysisContext ctx,
        IRecursivePatternOperation recursive,
        INamedTypeSymbol duAttr)
    {
        // The matched type is the union struct type
        var matchedType = recursive.MatchedType as INamedTypeSymbol;
        if (matchedType is null)
            return;

        var unionInfo = UnionTypeInfo.TryCreate(matchedType, duAttr);
        if (unionInfo is null || !unionInfo.IsStructUnion)
            return;

        // Find the kind subpattern to resolve variant name
        string? variantName = null;
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            if (!IsKindMemberOp(propSub))
                continue;

            variantName = ResolveVariantNameFromConstant(propSub.Pattern, unionInfo);
            break;
        }

        // Check each non-kind property subpattern for type mismatches
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            if (IsKindMemberOp(propSub))
                continue;

            var pattern = propSub.Pattern;

            switch (pattern)
            {
                case IDiscardPatternOperation:
                    // Untyped discard — always OK
                    continue;

                case IDeclarationPatternOperation declPattern:
                    // `var x` — infers from context, always OK
                    if (declPattern.MatchedType is null)
                        continue;
                    if (declPattern.InputType is not null &&
                        SymbolEqualityComparer.Default.Equals(
                            declPattern.InputType, declPattern.MatchedType))
                        continue;

                    // Has an explicit type — property patterns should use `var`
                    var memberName = GetPropertySubpatternMemberName(propSub);
                    var fieldIndex = GetFieldIndex(matchedType, variantName, memberName);

                    var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add("ExpectedType", "var");
                    properties.Add("FieldIndex", fieldIndex.ToString());

                    var actualTypeDisplay = declPattern.MatchedType?.ToDisplayString(
                        SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "?";

                    ctx.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
                        pattern.Syntax.GetLocation(),
                        properties.ToImmutable(),
                        variantName ?? matchedType.Name,
                        fieldIndex,
                        "var",
                        actualTypeDisplay));
                    break;

                default:
                    continue;
            }
        }
    }

    private static bool IsKindMemberOp(IPropertySubpatternOperation propSub)
    {
        if (propSub.Member is IPropertyReferenceOperation propRef)
            return propRef.Property.Name == "kind";
        if (propSub.Member is IFieldReferenceOperation fieldRef)
            return fieldRef.Field.Name == "kind";
        return false;
    }

    private static string? GetPropertySubpatternMemberName(IPropertySubpatternOperation propSub)
    {
        if (propSub.Member is IPropertyReferenceOperation propRef)
            return propRef.Property.Name;
        if (propSub.Member is IFieldReferenceOperation fieldRef)
            return fieldRef.Field.Name;
        return null;
    }

    private static int GetFieldIndex(
        INamedTypeSymbol unionType, string? variantName, string? memberName)
    {
        if (variantName is null || memberName is null)
            return 0;

        var factoryMethod = unionType.GetMembers(variantName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.MethodKind == MethodKind.Ordinary);

        if (factoryMethod is null)
            return 0;

        for (int i = 0; i < factoryMethod.Parameters.Length; i++)
        {
            if (factoryMethod.Parameters[i].Name == memberName ||
                memberName == FieldNameResolver.CamelCase(variantName) + "_" + factoryMethod.Parameters[i].Name)
                return i;
        }

        return 0;
    }

    /// Resolves the variant name from a constant pattern (Kind enum value).
    private static string? ResolveVariantNameFromConstant(
        IPatternOperation pattern, UnionTypeInfo unionInfo)
    {
        if (pattern is not IConstantPatternOperation constant)
            return null;

        var valueOp = constant.Value;
        if (valueOp is null || valueOp.Type is null)
            return null;

        var kindEnum = unionInfo.KindEnumType;
        if (kindEnum is null)
            return null;

        if (!SymbolEqualityComparer.Default.Equals(valueOp.Type, kindEnum))
            return null;

        if (!valueOp.ConstantValue.HasValue)
            return null;

        var value = valueOp.ConstantValue.Value;
        if (TryGetOrdinal(value, out int ordinal) && ordinal >= 0 && ordinal < unionInfo.VariantNames.Length)
            return unionInfo.VariantNames[ordinal];

        return null;
    }
}
