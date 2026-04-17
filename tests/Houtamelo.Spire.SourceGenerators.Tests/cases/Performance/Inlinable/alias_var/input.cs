using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Aliases
{
    public static void Run(int x, [Inlinable] Action<int> action)
    {
        var a = action;
        a(x);
    }
}
