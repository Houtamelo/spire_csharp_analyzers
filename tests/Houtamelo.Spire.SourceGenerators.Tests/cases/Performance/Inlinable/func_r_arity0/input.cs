using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Lazy
{
    public static R Get<R>([Inlinable] Func<R> factory)
    {
        return factory();
    }
}
