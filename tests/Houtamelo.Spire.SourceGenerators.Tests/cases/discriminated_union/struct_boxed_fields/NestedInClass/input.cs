using Houtamelo.Spire;

namespace TestNs
{
    public static partial class Unions
    {
        [DiscriminatedUnion(Layout.BoxedFields)]
        internal partial struct Token
        {
            [Variant] public static partial Token Ident(string name);
            [Variant] public static partial Token Number(int value);
        }
    }
}
