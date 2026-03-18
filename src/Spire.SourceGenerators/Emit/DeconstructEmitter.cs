using System.Collections.Generic;
using System.Linq;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators.Emit;

/// Generates Deconstruct overloads for BoxedFields (and later BoxedTuple/Overlap).
/// Groups variants by field count and emits typed or object? overloads.
internal static class DeconstructEmitter
{
    /// Emits Deconstruct overloads for the BoxedFields layout.
    /// Field access pattern: this._f{n} with casts for unique arity.
    public static void EmitBoxedFieldsDeconstructs(SourceBuilder sb, IEnumerable<VariantInfo> variants)
    {
        var variantList = variants.ToList();
        if (variantList.Count == 0) return;

        var groups = GroupByFieldCount(variantList);
        var hasFieldlessVariants = groups.ContainsKey(0);

        // If all variants are fieldless, emit a single (out Kind, out object?) that returns null.
        if (groups.Count == 1 && hasFieldlessVariants)
        {
            sb.AppendLine();
            sb.AppendLine("public void Deconstruct(out Kind kind, out object? _f0)");
            sb.OpenBrace();
            sb.AppendLine("kind = this.tag;");
            sb.AppendLine("_f0 = null;");
            sb.CloseBrace();
            return;
        }

        // For non-zero field count groups, determine shared vs unique.
        // Fieldless variants (0-field group) are NOT given their own Deconstruct —
        // they participate naturally via the smallest non-zero shared arity.
        var nonZeroGroups = groups
            .Where(g => g.Key > 0)
            .OrderBy(g => g.Key)
            .ToList();

        // If fieldless variants exist, merge them into the smallest non-zero group
        // to make it shared (if it isn't already).
        if (hasFieldlessVariants && nonZeroGroups.Count > 0)
        {
            var smallest = nonZeroGroups[0];
            // Add a dummy "variant" to force shared arity when the group has only 1 real variant
            if (smallest.Value.Count == 1)
            {
                // Mark as shared by adding fieldless variants
                smallest.Value.AddRange(groups[0]);
            }
        }

        foreach (var group in nonZeroGroups)
        {
            int fieldCount = group.Key;
            var groupVariants = group.Value;

            sb.AppendLine();

            if (groupVariants.Count == 1)
            {
                // Unique arity: typed Deconstruct
                var variant = groupVariants[0];
                EmitTypedDeconstruct(sb, variant);
            }
            else
            {
                // Shared arity: object? Deconstruct
                EmitObjectDeconstruct(sb, fieldCount);
            }
        }
    }

    private static void EmitTypedDeconstruct(SourceBuilder sb, VariantInfo variant)
    {
        var paramList = "out Kind kind";
        for (int i = 0; i < variant.Fields.Length; i++)
        {
            var field = variant.Fields[i];
            paramList += $", out {field.TypeFullName} {field.Name}";
        }

        sb.AppendLine($"public void Deconstruct({paramList})");
        sb.OpenBrace();
        sb.AppendLine("kind = this.tag;");
        for (int i = 0; i < variant.Fields.Length; i++)
        {
            var field = variant.Fields[i];
            sb.AppendLine($"{field.Name} = ({field.TypeFullName})this._f{i}!;");
        }
        sb.CloseBrace();
    }

    private static void EmitObjectDeconstruct(SourceBuilder sb, int fieldCount)
    {
        var paramList = "out Kind kind";
        for (int i = 0; i < fieldCount; i++)
        {
            paramList += $", out object? f{i}";
        }

        sb.AppendLine($"public void Deconstruct({paramList})");
        sb.OpenBrace();
        sb.AppendLine("kind = this.tag;");
        for (int i = 0; i < fieldCount; i++)
        {
            sb.AppendLine($"f{i} = this._f{i};");
        }
        sb.CloseBrace();
    }

    /// Groups variants by their field count.
    private static Dictionary<int, List<VariantInfo>> GroupByFieldCount(List<VariantInfo> variants)
    {
        var groups = new Dictionary<int, List<VariantInfo>>();
        foreach (var v in variants)
        {
            int count = v.Fields.Length;
            if (!groups.TryGetValue(count, out var list))
            {
                list = new List<VariantInfo>();
                groups[count] = list;
            }
            list.Add(v);
        }
        return groups;
    }
}
