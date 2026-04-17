using System;
using System.Collections.Generic;
using Houtamelo.Spire;

namespace TestNs;

public static partial class ListExt
{
    public static List<R> Map<T, R>(this List<T> list, [Inlinable] Func<T, R> selector)
    {
        var result = new List<R>(list.Count);
        foreach (var item in list)
            result.Add(selector(item));
        return result;
    }
}
