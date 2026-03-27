using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Abstract base for cross-product domains (tuples, property patterns).
/// Each has a set of named or positional "slots", each with its own IValueDomain.
///
/// The Maranget algorithm uses Split() and specialization but does NOT call
/// Subtract() or Complement() on structural domains — cross-product subtraction
/// is inherently complex and unnecessary for the algorithm.
internal abstract class StructuralDomain : IValueDomain
{
    protected ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> Slots { get; }
    protected bool HasWildcard { get; }
    public ITypeSymbol Type { get; }

    protected StructuralDomain(
        ITypeSymbol type,
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard)
    {
        Type = type;
        Slots = slots;
        HasWildcard = hasWildcard;
    }

    public bool IsEmpty
    {
        get
        {
            if (HasWildcard)
                return false;

            foreach (var (_, domain) in Slots)
            {
                if (domain.IsEmpty)
                    return true;
            }

            return false;
        }
    }

    public bool IsUniverse
    {
        get
        {
            if (HasWildcard)
                return true;

            foreach (var (_, domain) in Slots)
            {
                if (!domain.IsUniverse)
                    return false;
            }

            return true;
        }
    }

    public virtual ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        // Find first slot that is not universe
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].Domain.IsUniverse)
                continue;

            var partitions = Slots[i].Domain.Split();
            var builder = ImmutableArray.CreateBuilder<IValueDomain>(partitions.Length);

            foreach (var partition in partitions)
            {
                var newSlots = Slots.SetItem(i, (Slots[i].Slot, partition));
                builder.Add(CreateDerived(newSlots, hasWildcard: false));
            }

            return builder.MoveToImmutable();
        }

        // All slots are universe — return array with just this
        return ImmutableArray.Create<IValueDomain>(this);
    }

    public IValueDomain Subtract(IValueDomain other)
        => throw new NotSupportedException(
            "Subtract is not supported on structural domains. The Maranget algorithm uses Split() instead.");

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not StructuralDomain otherStructural)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with {GetType().Name}");

        if (Slots.Length != otherStructural.Slots.Length)
            throw new ArgumentException("Cannot intersect structural domains with different slot counts");

        var builder = ImmutableArray.CreateBuilder<(SlotIdentifier, IValueDomain)>(Slots.Length);

        for (int i = 0; i < Slots.Length; i++)
        {
            var intersected = Slots[i].Domain.Intersect(otherStructural.Slots[i].Domain);
            builder.Add((Slots[i].Slot, intersected));
        }

        return CreateDerived(builder.MoveToImmutable(), hasWildcard: HasWildcard && otherStructural.HasWildcard);
    }

    public IValueDomain Complement()
        => throw new NotSupportedException(
            "Complement is not supported on structural domains. The Maranget algorithm uses Split() instead.");

    /// Create a new instance of the same concrete type with the given slots.
    protected abstract StructuralDomain CreateDerived(
        ImmutableArray<(SlotIdentifier Slot, IValueDomain Domain)> slots,
        bool hasWildcard);
}
