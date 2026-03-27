using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains.DiscriminatedUnion;

/// TupleDomain specialized for Spire discriminated unions.
/// Element[0] is always the Kind enum. Split() partitions on Kind
/// regardless of whether it is universe, ensuring one partition per variant.
internal sealed class DUTupleDomain : TupleDomain
{
    public INamedTypeSymbol KindEnumType { get; }
    public ImmutableArray<string> VariantNames { get; }

    public DUTupleDomain(
        ITypeSymbol type,
        INamedTypeSymbol kindEnumType,
        ImmutableArray<string> variantNames,
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        : base(type, slots, hasWildcard)
    {
        KindEnumType = kindEnumType;
        VariantNames = variantNames;
    }

    protected override StructuralDomain CreateDerived(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        => new DUTupleDomain(Type, KindEnumType, VariantNames, slots, hasWildcard);

    /// Always partition on the Kind slot (element[0]), producing one domain per remaining variant.
    public override ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        // Slot 0 is the Kind enum — split on it unconditionally
        var kindDomain = Slots[0].Domain;
        var partitions = kindDomain.Split();

        if (partitions.Length == 0)
            return ImmutableArray<IValueDomain>.Empty;

        var builder = ImmutableArray.CreateBuilder<IValueDomain>(partitions.Length);

        foreach (var partition in partitions)
        {
            var newSlots = Slots.SetItem(0, (Slots[0].Slot, partition));
            builder.Add(CreateDerived(newSlots, hasWildcard: false));
        }

        return builder.MoveToImmutable();
    }
}
