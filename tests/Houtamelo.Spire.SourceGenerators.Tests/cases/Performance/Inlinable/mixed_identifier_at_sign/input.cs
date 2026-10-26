using System;
using Houtamelo.Spire;

namespace Spelling;

public static partial class Targets
{
    public static int AtSign([Inlinable] Func<int, int> @callback, int value)
        => callback(value);
}
