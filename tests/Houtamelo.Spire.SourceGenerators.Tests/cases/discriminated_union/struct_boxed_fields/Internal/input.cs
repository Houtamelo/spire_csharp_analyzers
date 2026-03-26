using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    internal partial struct Internal
    {
        [Variant] public static partial Internal X(int v);
    }
}
