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

    /// Global R1 fields (one per unique unmanaged field name)
    public List<GlobalFieldInfo> GlobalR1 = new List<GlobalFieldInfo>();
    public List<GlobalFieldInfo> GlobalR2 = new List<GlobalFieldInfo>();
    public List<GlobalFieldInfo> GlobalR3 = new List<GlobalFieldInfo>();
}

/// Field info for global (name-pinned) mode.
internal sealed class GlobalFieldInfo
{
    public string Name = "";
    public string TypeFullName = "";
    /// Byte offset within the struct (R1) or slot index (R2/R3).
    public int Offset;
    /// Size in bytes (R1 only).
    public int Size;
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

    public static OverlapLayoutInfo ComputeLayout(
        IEnumerable<VariantInfo> variants, int kindSize)
    {
        var variantList = variants.ToList();
        return ComputeLayoutPinned(variantList, kindSize);
    }

    /// Name-pinned layout. Each unique field name gets a dedicated offset/slot.
    private static OverlapLayoutInfo ComputeLayoutPinned(List<VariantInfo> variantList, int kindSize)
    {
        var layout = new OverlapLayoutInfo();

        // Collect unique fields by region, keyed by name (ambiguity already rejected)
        var r1Fields = new Dictionary<string, FieldInfo>();
        var r2Fields = new Dictionary<string, FieldInfo>();
        var r3Fields = new Dictionary<string, FieldInfo>();

        foreach (var variant in variantList)
        {
            foreach (var field in variant.Fields)
            {
                var region = FieldClassifier.Classify(field);
                switch (region)
                {
                    case FieldRegion.Unmanaged:
                        if (!r1Fields.ContainsKey(field.Name))
                            r1Fields[field.Name] = field;
                        break;
                    case FieldRegion.Reference:
                        if (!r2Fields.ContainsKey(field.Name))
                            r2Fields[field.Name] = field;
                        break;
                    case FieldRegion.Boxed:
                        if (!r3Fields.ContainsKey(field.Name))
                            r3Fields[field.Name] = field;
                        break;
                }
            }
        }

        // R1: assign byte offsets sorted by name
        int r1Offset = kindSize;
        foreach (var kv in r1Fields.OrderBy(x => x.Key))
        {
            layout.GlobalR1.Add(new GlobalFieldInfo
            {
                Name = kv.Key,
                TypeFullName = kv.Value.TypeFullName,
                Offset = r1Offset,
                Size = kv.Value.KnownSize!.Value,
            });
            r1Offset += kv.Value.KnownSize!.Value;
        }
        layout.R1Size = r1Offset - kindSize;

        // R2: assign slot indices sorted by name
        layout.R2Offset = AlignUp(r1Offset, 8);
        int r2Slot = 0;
        foreach (var kv in r2Fields.OrderBy(x => x.Key))
        {
            layout.GlobalR2.Add(new GlobalFieldInfo
            {
                Name = kv.Key,
                TypeFullName = kv.Value.TypeFullName,
                Offset = r2Slot,
            });
            r2Slot++;
        }
        layout.R2Slots = r2Slot;

        // R3: assign slot indices sorted by name
        layout.R3Offset = layout.R2Offset + r2Slot * 8;
        int r3Slot = 0;
        foreach (var kv in r3Fields.OrderBy(x => x.Key))
        {
            layout.GlobalR3.Add(new GlobalFieldInfo
            {
                Name = kv.Key,
                TypeFullName = kv.Value.TypeFullName,
                Offset = r3Slot,
            });
            r3Slot++;
        }
        layout.R3Slots = r3Slot;

        // Build per-variant mappings referencing global fields
        foreach (var variant in variantList)
        {
            var vl = new VariantLayoutInfo();

            foreach (var field in variant.Fields)
            {
                var region = FieldClassifier.Classify(field);
                switch (region)
                {
                    case FieldRegion.Unmanaged:
                        vl.R1Fields.Add(new FieldPlacement { Field = field });
                        break;
                    case FieldRegion.Reference:
                        var r2Global = layout.GlobalR2.First(g => g.Name == field.Name);
                        vl.R2Fields.Add(new FieldPlacement { Field = field, SlotIndex = r2Global.Offset });
                        break;
                    case FieldRegion.Boxed:
                        var r3Global = layout.GlobalR3.First(g => g.Name == field.Name);
                        vl.R3Fields.Add(new FieldPlacement { Field = field, SlotIndex = r3Global.Offset });
                        break;
                }
            }

            layout.Variants[variant.Name] = vl;
        }

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
