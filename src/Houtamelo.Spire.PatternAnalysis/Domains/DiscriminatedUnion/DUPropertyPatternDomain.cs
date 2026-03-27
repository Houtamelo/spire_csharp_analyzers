using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains.DiscriminatedUnion;

/// PropertyPatternDomain specialized for Spire discriminated unions.
/// Knows about the `kind` property and partitions on it during Split(),
/// ensuring one partition per variant.
internal sealed class DUPropertyPatternDomain : PropertyPatternDomain
{
    public IPropertySymbol KindProperty { get; }
    public INamedTypeSymbol KindEnumType { get; }
    public ImmutableArray<string> VariantNames { get; }

    readonly int _kindSlotIndex;

    public DUPropertyPatternDomain(
        ITypeSymbol type,
        IPropertySymbol kindProperty,
        INamedTypeSymbol kindEnumType,
        ImmutableArray<string> variantNames,
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        : base(type, slots, hasWildcard)
    {
        KindProperty = kindProperty;
        KindEnumType = kindEnumType;
        VariantNames = variantNames;
        _kindSlotIndex = FindKindSlotIndex(slots, kindProperty);
    }

    static int FindKindSlotIndex(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        IPropertySymbol kindProperty)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Slot is SlotIdentifier.PropertySlot ps
                && SymbolEqualityComparer.Default.Equals(ps.Property, kindProperty))
            {
                return i;
            }
        }

        throw new ArgumentException(
            $"No slot found for kind property '{kindProperty.Name}' in the provided slots.");
    }

    protected override StructuralDomain CreateDerived(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        => new DUPropertyPatternDomain(Type, KindProperty, KindEnumType, VariantNames, slots, hasWildcard);

    /// Always partition on the kind property slot, producing one domain per remaining variant.
    public override ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        var kindDomain = Slots[_kindSlotIndex].Domain;
        var partitions = kindDomain.Split();

        if (partitions.Length == 0)
            return ImmutableArray<IValueDomain>.Empty;

        var builder = ImmutableArray.CreateBuilder<IValueDomain>(partitions.Length);

        foreach (var partition in partitions)
        {
            var newSlots = Slots.SetItem(_kindSlotIndex, (Slots[_kindSlotIndex].Slot, partition));
            builder.Add(CreateDerived(newSlots, hasWildcard: false));
        }

        return builder.MoveToImmutable();
    }
}
