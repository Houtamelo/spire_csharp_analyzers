using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain representing a set of named enum members.
internal sealed class EnumDomain : IValueDomain
{
    readonly ImmutableHashSet<IFieldSymbol> _members;
    readonly ImmutableHashSet<IFieldSymbol> _allMembers;

    public ITypeSymbol Type { get; }

    public EnumDomain(ITypeSymbol type, ImmutableHashSet<IFieldSymbol> members, ImmutableHashSet<IFieldSymbol> allMembers)
    {
        Type = type;
        _members = members;
        _allMembers = allMembers;
    }

    public static EnumDomain Universe(INamedTypeSymbol enumType)
    {
        var allMembers = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);

        return new EnumDomain(enumType, allMembers, allMembers);
    }

    public bool IsEmpty => _members.Count == 0;

    public bool IsUniverse => _members.Count == _allMembers.Count
        && _allMembers.All(m => _members.Contains(m));

    public ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        var builder = ImmutableArray.CreateBuilder<IValueDomain>(_members.Count);

        foreach (var member in _members)
        {
            var singleton = ImmutableHashSet.Create<IFieldSymbol>(SymbolEqualityComparer.Default, member);
            builder.Add(new EnumDomain(Type, singleton, _allMembers));
        }

        return builder.MoveToImmutable();
    }

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is not EnumDomain otherEnum)
            throw new ArgumentException($"Cannot subtract {other.GetType().Name} from EnumDomain");

        return new EnumDomain(Type, _members.Except(otherEnum._members), _allMembers);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not EnumDomain otherEnum)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with EnumDomain");

        return new EnumDomain(Type, _members.Intersect(otherEnum._members), _allMembers);
    }

    public IValueDomain Complement() => new EnumDomain(Type, _allMembers.Except(_members), _allMembers);
}
