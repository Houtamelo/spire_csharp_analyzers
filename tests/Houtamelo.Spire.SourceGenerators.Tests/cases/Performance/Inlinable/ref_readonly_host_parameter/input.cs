using System;
using Houtamelo.Spire;

namespace TestNs
{
    public static partial class Runner
    {
        public static void Run(ref readonly int value, [Inlinable] Action action)
        {
            action();
        }
    }
}
