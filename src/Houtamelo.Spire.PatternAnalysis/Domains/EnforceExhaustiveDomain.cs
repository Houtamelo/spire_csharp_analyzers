using System;
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain representing the set of concrete derived/implementing types
/// of an [EnforceExhaustiveness]-marked base type.
/// Each type in the set is a "variant" that must be covered.
internal sealed class EnforceExhaustiveDomain : IValueDomain
{
    readonly ITypeSymbol _baseType;
    readonly ImmutableHashSet<INamedTypeSymbol> _remainingTypes;
    readonly ImmutableHashSet<INamedTypeSymbol> _allTypes;

    public ITypeSymbol Type => _baseType;

    /// Exposed for tests that need to construct partial domains with the same allTypes set.
    internal ImmutableHashSet<INamedTypeSymbol> AllTypes => _allTypes;

    public EnforceExhaustiveDomain(
        ITypeSymbol baseType,
        ImmutableHashSet<INamedTypeSymbol> remainingTypes,
        ImmutableHashSet<INamedTypeSymbol> allTypes)
    {
        _baseType = baseType;
        _remainingTypes = remainingTypes;
        _allTypes = allTypes;
    }

    public static EnforceExhaustiveDomain Create(
        INamedTypeSymbol baseType,
        TypeHierarchyResolver resolver,
        Compilation compilation)
    {
        var concreteTypes = resolver.Resolve(baseType, compilation);
        var allTypes = concreteTypes.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        return new EnforceExhaustiveDomain(baseType, allTypes, allTypes);
    }

    public bool IsEmpty => _remainingTypes.Count == 0;

    public bool IsUniverse => _remainingTypes.Count == _allTypes.Count;

    public ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        var builder = ImmutableArray.CreateBuilder<IValueDomain>(_remainingTypes.Count);

        foreach (var type in _remainingTypes)
        {
            var singleton = ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, type);
            builder.Add(new EnforceExhaustiveDomain(_baseType, singleton, _allTypes));
        }

        return builder.MoveToImmutable();
    }

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is not EnforceExhaustiveDomain otherDomain)
            throw new ArgumentException($"Cannot subtract {other.GetType().Name} from EnforceExhaustiveDomain");

        return new EnforceExhaustiveDomain(_baseType, _remainingTypes.Except(otherDomain._remainingTypes), _allTypes);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not EnforceExhaustiveDomain otherDomain)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with EnforceExhaustiveDomain");

        return new EnforceExhaustiveDomain(_baseType, _remainingTypes.Intersect(otherDomain._remainingTypes), _allTypes);
    }

    public IValueDomain Complement()
        => new EnforceExhaustiveDomain(_baseType, _allTypes.Except(_remainingTypes), _allTypes);
}
