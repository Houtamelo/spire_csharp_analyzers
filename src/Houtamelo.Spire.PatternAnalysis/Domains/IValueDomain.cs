using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Interface for a set of possible values of a given type.
/// Each domain represents the "universe" of values that a pattern slot can hold,
/// and supports set operations needed by the Maranget algorithm.
internal interface IValueDomain
{
    /// True when no values remain in this domain.
    bool IsEmpty { get; }

    /// True when this domain contains every possible value of its type.
    bool IsUniverse { get; }

    /// The C# type this domain represents.
    ITypeSymbol Type { get; }

    /// Remove values covered by other from this domain.
    IValueDomain Subtract(IValueDomain other);

    /// Values present in both this and other.
    IValueDomain Intersect(IValueDomain other);

    /// All values NOT in this domain (relative to universe of this type).
    IValueDomain Complement();

    /// Decompose into disjoint partitions for Maranget column specialization.
    /// Each partition represents a distinct "case" the algorithm must check.
    ImmutableArray<IValueDomain> Split();
}
