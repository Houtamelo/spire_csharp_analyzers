using System;
using Houtamelo.Spire;

namespace Collision.Beta;

public static partial class Host
{
    public static int Run([Inlinable] Func<int, int> callback, int value)
        => callback(value);
}
