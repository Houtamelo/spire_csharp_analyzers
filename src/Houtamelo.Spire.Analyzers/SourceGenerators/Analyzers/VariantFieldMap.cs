using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Maps generated variant field/property names to their owning variant name(s)
/// for struct discriminated unions.
internal sealed class VariantFieldMap
{
    private readonly Dictionary<string, ImmutableHashSet<string>> _memberToVariants;

    private VariantFieldMap(Dictionary<string, ImmutableHashSet<string>> map)
        => _memberToVariants = map;

    /// Returns all variants that expose this field/property, or an empty set if
    /// it is not a generated variant member.
    public ImmutableHashSet<string> GetOwningVariants(string memberName)
    {
        return _memberToVariants.TryGetValue(memberName, out var variants)
            ? variants
            : ImmutableHashSet<string>.Empty;
    }

    /// Builds the map from a union type's fields, properties, and Kind enum members.
    public static VariantFieldMap? TryCreate(INamedTypeSymbol unionType, UnionTypeInfo info)
    {
        if (!info.IsStructUnion) return null;

        var map = new Dictionary<string, ImmutableHashSet<string>>();

        // Scan [EditorBrowsable(Never)] properties for variant field info
        // These have bare field names. Map each property to its owning variant
        // by checking which [Variant] methods declare a parameter with that name.
        ScanProperties(unionType, info, map);

        // Old mode: scan [EditorBrowsable(Never)] fields with variant-prefixed names
        ScanFields(unionType, info, map);

        return new VariantFieldMap(map);
    }

    private static void ScanProperties(
        INamedTypeSymbol unionType, UnionTypeInfo info,
        Dictionary<string, ImmutableHashSet<string>> map)
    {
        // Find all [Variant] methods to build parameter→variant mapping
        var variantMethods = unionType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && HasVariantAttribute(m))
            .ToList();

        // Resolve each generated property from the original factory parameter.
        // Qualified properties are named camelCaseVariant_field.
        foreach (var prop in unionType.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.Name == "kind") continue;
            if (!HasEditorBrowsableNever(prop)) continue;

            foreach (var method in variantMethods)
            {
                if (method.Parameters.Any(parameter =>
                    parameter.Name == prop.Name ||
                    prop.Name == ToCamelCase(method.Name) + "_" + parameter.Name))
                {
                    Add(map, prop.Name, method.Name);
                }
            }
        }
    }

    private static void ScanFields(
        INamedTypeSymbol unionType, UnionTypeInfo info,
        Dictionary<string, ImmutableHashSet<string>> map)
    {
        var variantCamelNames = info.VariantNames
            .Select(v => (Original: v, CamelCase: ToCamelCase(v)))
            .ToList();

        foreach (var field in unionType.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.Name == "kind") continue;
            if (!HasEditorBrowsableNever(field)) continue;
            if (field.Name.StartsWith("_obj_")) continue;
            if (field.Name.StartsWith("_")) continue; // skip internal storage fields in global mode

            // Already mapped by property scan
            if (map.ContainsKey(field.Name)) continue;

            foreach (var (original, camel) in variantCamelNames)
            {
                if (field.Name == camel || field.Name.StartsWith(camel + "_"))
                {
                    Add(map, field.Name, original);
                    break;
                }
            }
        }
    }

    private static void Add(
        Dictionary<string, ImmutableHashSet<string>> map,
        string memberName,
        string variantName)
    {
        if (!map.TryGetValue(memberName, out var variants))
            variants = ImmutableHashSet<string>.Empty;
        map[memberName] = variants.Add(variantName);
    }

    private static bool HasVariantAttribute(IMethodSymbol method)
    {
        return method.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "VariantAttribute");
    }

    private static bool HasEditorBrowsableNever(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "EditorBrowsableAttribute" &&
            a.ConstructorArguments.Length > 0 &&
            a.ConstructorArguments[0].Value is int val &&
            val == 1); // EditorBrowsableState.Never = 1
    }

    private static string ToCamelCase(string name)
        => FieldNameResolver.CamelCase(name);
}
