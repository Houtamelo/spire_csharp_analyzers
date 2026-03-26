using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Maps generated variant field/property names to their owning variant name(s)
/// for struct discriminated unions.
internal sealed class VariantFieldMap
{
    private readonly Dictionary<string, string> _memberToVariant;

    private VariantFieldMap(Dictionary<string, string> map) => _memberToVariant = map;

    /// Returns the variant name that owns this field/property, or null if not a variant member.
    public string? GetOwningVariant(string memberName)
    {
        _memberToVariant.TryGetValue(memberName, out var variant);
        return variant;
    }

    /// Builds the map from a union type's fields, properties, and Kind enum members.
    public static VariantFieldMap? TryCreate(INamedTypeSymbol unionType, UnionTypeInfo info)
    {
        if (!info.IsStructUnion) return null;

        var map = new Dictionary<string, string>();

        // Scan [EditorBrowsable(Never)] properties for variant field info
        // These have bare field names. Map each property to its owning variant
        // by checking which [Variant] methods declare a parameter with that name.
        ScanProperties(unionType, info, map);

        // Old mode: scan [EditorBrowsable(Never)] fields with variant-prefixed names
        ScanFields(unionType, info, map);

        return new VariantFieldMap(map);
    }

    private static void ScanProperties(
        INamedTypeSymbol unionType, UnionTypeInfo info, Dictionary<string, string> map)
    {
        // Find all [Variant] methods to build parameter→variant mapping
        var variantMethods = unionType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && HasVariantAttribute(m))
            .ToList();

        // For each [EditorBrowsable(Never)] property, find which variant owns it
        foreach (var prop in unionType.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.Name == "kind") continue;
            if (!HasEditorBrowsableNever(prop)) continue;

            // Find the first variant method that has a parameter matching this property name
            foreach (var method in variantMethods)
            {
                if (method.Parameters.Any(p => p.Name == prop.Name))
                {
                    if (!map.ContainsKey(prop.Name))
                        map[prop.Name] = method.Name;
                    break;
                }
            }
        }
    }

    private static void ScanFields(
        INamedTypeSymbol unionType, UnionTypeInfo info, Dictionary<string, string> map)
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
                    map[field.Name] = original;
                    break;
                }
            }
        }
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
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
