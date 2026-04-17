using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Aliases
{
    public static void Run(bool cond, int x, [Inlinable] Action<int> action)
    {
        var a = action;
        var b = action;
        var c = cond ? a : b;
        c(x);
    }
}
