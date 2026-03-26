using System;
using System.Collections.Generic;

namespace Houtamelo.Spire.Core;

public static class SpireLINQ
{
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
