using System;
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

        // Scan generated [EditorBrowsable(Never)] variant accessors. Payload
        // fields are declared under the sibling types container, not on the union.
        ScanProperties(unionType, info, map);

        return new VariantFieldMap(map);
    }

    private static void ScanProperties(
        INamedTypeSymbol unionType, UnionTypeInfo info,
        Dictionary<string, ImmutableHashSet<string>> map)
    {
        foreach (var prop in unionType.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.Name == "kind") continue;
            if (!HasEditorBrowsableNever(prop)) continue;

            // The variant-payload representation exposes one hidden accessor per
            // variant (for example `move` or `run_value`). Its name is derived
            // from the factory name and it owns the entire payload, rather than
            // one of the factory parameters.
            foreach (var variantName in info.VariantNames)
            {
                var accessorName = VariantNaming.AccessorName(
                    variantName,
                    info.VariantNames);
                if (string.Equals(prop.Name, accessorName, StringComparison.Ordinal))
                    Add(map, prop.Name, variantName);
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

    private static bool HasEditorBrowsableNever(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "EditorBrowsableAttribute" &&
            a.ConstructorArguments.Length > 0 &&
            a.ConstructorArguments[0].Value is int val &&
            val == 1); // EditorBrowsableState.Never = 1
    }

}
