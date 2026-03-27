using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Structural domain for tuple patterns. Slots are positional (TupleSlot).
internal class TupleDomain : StructuralDomain
{
    public TupleDomain(
        ITypeSymbol type,
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        : base(type, slots, hasWildcard)
    {
    }

    protected override StructuralDomain CreateDerived(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        => new TupleDomain(Type, slots, hasWildcard);
}
