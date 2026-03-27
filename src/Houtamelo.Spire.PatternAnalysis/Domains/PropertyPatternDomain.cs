using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Structural domain for property patterns. Slots are named (PropertySlot).
/// Only tracks properties mentioned in switch arms — unmentioned properties
/// are implicitly wildcarded.
internal class PropertyPatternDomain : StructuralDomain
{
    public PropertyPatternDomain(
        ITypeSymbol type,
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        : base(type, slots, hasWildcard)
    {
    }

    protected override StructuralDomain CreateDerived(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
        => new PropertyPatternDomain(Type, slots, hasWildcard);
}
