using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Runner
{
    public static void Repeat(int n, [Inlinable] Action cb)
    {
        for (int i = 0; i < n; i++)
            cb();
    }
}
