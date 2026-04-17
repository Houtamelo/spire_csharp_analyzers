using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Runner
{
    public static void Run(
        [Inlinable] Action<int, int, int, int, int, int, int, int> cb,
        int a, int b, int c, int d, int e, int f, int g, int h)
    {
        cb(a, b, c, d, e, f, g, h);
    }
}
