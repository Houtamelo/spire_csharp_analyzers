using System;
using System.Collections.Generic;
using Houtamelo.Spire;

namespace TestNs;

public static partial class ListExt
{
    public static void ForEach<T>(this List<T> list, [Inlinable] Action<T> action)
    {
        foreach (var item in list)
            action(item);
    }
}
