using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Sentinel domain representing an empty value set.
/// Used as the inner domain for null-only NullableDomains when the
/// real inner domain type does not support Subtract (e.g., StructuralDomain).
internal sealed class EmptyDomain : IValueDomain
{
    public ITypeSymbol Type { get; }

    public EmptyDomain(ITypeSymbol type)
    {
        Type = type;
    }

    public bool IsEmpty => true;

    public bool IsUniverse => false;

    public IValueDomain Subtract(IValueDomain other) => this;

    public IValueDomain Intersect(IValueDomain other) => this;

    public IValueDomain Complement()
        => throw new System.NotSupportedException(
            "Complement of EmptyDomain is not meaningful without knowing the universe.");

    public ImmutableArray<IValueDomain> Split() => ImmutableArray<IValueDomain>.Empty;
}
