using System.Collections.Generic;
using System.Linq;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators.Emit;

internal sealed class OverlapLayoutInfo
{
    public int R1Size;
    public int R2Offset;
    public int R2Slots;
    public int R3Offset;
    public int R3Slots;

    /// Per-variant field placements
    public Dictionary<string, VariantLayoutInfo> Variants = new Dictionary<string, VariantLayoutInfo>();
}

internal sealed class VariantLayoutInfo
{
    public List<FieldPlacement> R1Fields = new List<FieldPlacement>();
    public List<FieldPlacement> R2Fields = new List<FieldPlacement>();
    public List<FieldPlacement> R3Fields = new List<FieldPlacement>();

    /// If there are multiple R1 fields, they are stored as a single ValueTuple
    public bool R1IsTuple;
    /// The name of the R1 tuple field (variantCamelCase), or the single field name
    public string R1FieldName = "";
}

internal sealed class FieldPlacement
{
    public FieldInfo Field = null!;
    public int SlotIndex; // For R2/R3: which slot index this field uses
}

internal static class OverlapLayoutComputer
{
    /// Returns the smallest unsigned integer type name for the Kind enum.
    public static string KindTypeName(int variantCount)
    {
        if (variantCount <= 255) return "byte";
        if (variantCount <= 65535) return "ushort";
        return "uint";
    }

    /// Returns sizeof for the Kind enum's underlying type.
    public static int KindSize(int variantCount)
    {
        if (variantCount <= 255) return 1;
        if (variantCount <= 65535) return 2;
        return 4;
    }

    public static OverlapLayoutInfo ComputeLayout(IEnumerable<VariantInfo> variants, int kindSize)
    {
        var layout = new OverlapLayoutInfo();
        var variantList = variants.ToList();

        int maxR1Size = 0;
        int maxR2Slots = 0;
        int maxR3Slots = 0;

        foreach (var variant in variantList)
        {
            var vl = new VariantLayoutInfo();
            int r2Count = 0;
            int r3Count = 0;

            foreach (var field in variant.Fields)
            {
                var region = FieldClassifier.Classify(field);
                switch (region)
                {
                    case FieldRegion.Unmanaged:
                        vl.R1Fields.Add(new FieldPlacement { Field = field, SlotIndex = 0 });
                        break;
                    case FieldRegion.Reference:
                        vl.R2Fields.Add(new FieldPlacement { Field = field, SlotIndex = r2Count++ });
                        break;
                    case FieldRegion.Boxed:
                        vl.R3Fields.Add(new FieldPlacement { Field = field, SlotIndex = r3Count++ });
                        break;
                }
            }

            // Compute R1 size for this variant
            int r1Size;
            if (vl.R1Fields.Count == 0)
            {
                r1Size = 0;
                vl.R1IsTuple = false;
                vl.R1FieldName = "";
            }
            else if (vl.R1Fields.Count == 1)
            {
                r1Size = vl.R1Fields[0].Field.KnownSize!.Value;
                vl.R1IsTuple = false;
                vl.R1FieldName = ToCamelCase(variant.Name) + "_" + vl.R1Fields[0].Field.Name;
            }
            else
            {
                r1Size = 0;
                foreach (var fp in vl.R1Fields)
                    r1Size += fp.Field.KnownSize!.Value;
                vl.R1IsTuple = true;
                vl.R1FieldName = ToCamelCase(variant.Name);
            }

            if (r1Size > maxR1Size) maxR1Size = r1Size;
            if (r2Count > maxR2Slots) maxR2Slots = r2Count;
            if (r3Count > maxR3Slots) maxR3Slots = r3Count;

            layout.Variants[variant.Name] = vl;
        }

        layout.R1Size = maxR1Size;
        layout.R2Offset = AlignUp(kindSize + maxR1Size, 8);
        layout.R2Slots = maxR2Slots;
        layout.R3Offset = layout.R2Offset + maxR2Slots * 8;
        layout.R3Slots = maxR3Slots;

        return layout;
    }

    public static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) / alignment * alignment;
    }

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
