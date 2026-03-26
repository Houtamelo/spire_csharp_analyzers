using System;
using System.Collections.Immutable;

namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

public readonly struct KindState : IEquatable<KindState>
{
    public ImmutableHashSet<string>? Variants { get; }
    public bool IsKnown => Variants is not null;

    private KindState(ImmutableHashSet<string>? variants) => Variants = variants;

    public static readonly KindState Unknown = new(null);

    public static KindState Known(ImmutableHashSet<string> variants) => new(variants);

    public static KindState Merge(KindState a, KindState b)
    {
        if (!a.IsKnown || !b.IsKnown) return Unknown;
        return Known(a.Variants!.Union(b.Variants!));
    }

    public bool Equals(KindState other)
    {
        if (IsKnown != other.IsKnown) return false;
        if (!IsKnown) return true;
        return Variants!.SetEquals(other.Variants!);
    }

    public override bool Equals(object? obj) => obj is KindState other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsKnown) return 0;
        var hash = 0;
        foreach (var v in Variants!)
            hash ^= v.GetHashCode();
        return hash;
    }

    public static bool operator ==(KindState left, KindState right) => left.Equals(right);
    public static bool operator !=(KindState left, KindState right) => !left.Equals(right);
}
