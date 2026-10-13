//@ should_fail
// SPIRE025: [Inlinable] ref readonly parameter

using System;
using Houtamelo.Spire;

namespace TestNs
{
    public static partial class Samples
    {
        public static void Take([Inlinable] ref readonly Action action) //~ ERROR
        {
            action();
        }
    }
}
