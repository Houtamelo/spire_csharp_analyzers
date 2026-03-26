//@ should_pass
// Accessing .kind in a lambda — kind is always safe, not a variant field
using System;
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Word(string wordText);
        [Variant] public static partial Token Number(double numberVal);
    }
    class C
    {
        void Test()
        {
            Func<Token, Token.Kind> f = s => s.kind;
        }
    }
}
