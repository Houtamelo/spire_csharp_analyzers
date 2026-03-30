using System;
using System.Collections.Generic;

namespace Houtamelo.Spire;

/// <summary>
/// LINQ extensions for discriminated unions.
/// </summary>
public static class SpireLINQ
{
    /// <summary>
    /// Filters a sequence of discriminated unions to only those whose <c>kind</c>
    /// matches the specified variant.
    /// </summary>
    /// <typeparam name="TDU">The discriminated union type.</typeparam>
    /// <typeparam name="TEnum">The <c>Kind</c> enum type.</typeparam>
    /// <param name="source">The sequence to filter.</param>
    /// <param name="kind">The variant to match.</param>
    /// <returns>Elements whose <c>kind</c> equals <paramref name="kind"/>.</returns>
    public static IEnumerable<TDU> OfKind<TDU, TEnum>(
        this IEnumerable<TDU> source, TEnum kind)
        where TDU : IDiscriminatedUnion<TEnum>
        where TEnum : Enum
    {
        foreach (var item in source)
        {
            if (EqualityComparer<TEnum>.Default.Equals(item.kind, kind))
                yield return item;
        }
    }
}
