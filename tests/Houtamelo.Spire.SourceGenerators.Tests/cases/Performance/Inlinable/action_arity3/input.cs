using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Runner
{
    public static void Run([Inlinable] Action<int, int, int> cb, int a, int b, int c)
    {
        cb(a, b, c);
    }
}
