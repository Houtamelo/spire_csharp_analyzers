using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Notifier
{
    public static void Fire(int x, [Inlinable] Action<int>? cb)
    {
        cb?.Invoke(x);
    }
}
