//@ should_fail
// SPIRE014: variant field accessed in lambda with no kind guard
using System;
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Option
    {
        [Variant] public static partial Option Some(double someValue);
        [Variant] public static partial Option None(int noneCode);
    }
    class C
    {
        void Test()
        {
            Func<Option, double> f = s => s.someValue; //~ ERROR
        }
    }
}
