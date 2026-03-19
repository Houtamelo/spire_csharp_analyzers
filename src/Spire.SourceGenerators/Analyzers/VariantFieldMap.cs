using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Spire.SourceGenerators.Analyzers;

/// Maps generated variant field names to their owning variant name for struct discriminated unions.
internal sealed class VariantFieldMap
{
    private readonly Dictionary<string, string> _fieldToVariant;

    private VariantFieldMap(Dictionary<string, string> map) => _fieldToVariant = map;

    /// Returns the variant name that owns this field, or null if not a variant field.
    public string? GetOwningVariant(string fieldName)
    {
        _fieldToVariant.TryGetValue(fieldName, out var variant);
        return variant;
    }

    /// Builds the map from a union type's fields and Kind enum members.
    public static VariantFieldMap? TryCreate(INamedTypeSymbol unionType, UnionTypeInfo info)
    {
        if (!info.IsStructUnion) return null;

        var map = new Dictionary<string, string>();
        var variantCamelNames = info.VariantNames
            .Select(v => (Original: v, CamelCase: ToCamelCase(v)))
            .ToList();

        // Scan all fields on the struct
        foreach (var field in unionType.GetMembers().OfType<IFieldSymbol>())
        {
            // Skip tag field and non-variant fields
            if (field.Name == "tag") continue;
            if (!HasEditorBrowsableNever(field)) continue;
            if (field.Name.StartsWith("_obj_")) continue; // shared Region 3

            // Match field name to variant
            foreach (var (original, camel) in variantCamelNames)
            {
                if (field.Name == camel || field.Name.StartsWith(camel + "_"))
                {
                    map[field.Name] = original;
                    break;
                }
            }
        }

        return new VariantFieldMap(map);
    }

    private static bool HasEditorBrowsableNever(IFieldSymbol field)
    {
        return field.GetAttributes().Any(a =>
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
