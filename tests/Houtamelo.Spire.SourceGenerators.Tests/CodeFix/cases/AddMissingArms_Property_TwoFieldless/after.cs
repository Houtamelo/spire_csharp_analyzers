using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Bit
    {
        [Variant] public static partial Bit Yes();
        [Variant] public static partial Bit No();
    }

    class Consumer
    {
        int Test(Bit b) => b switch
        {
            { kind: Bit.Kind.Yes } => 1,
            { kind: Bit.Kind.No } => throw new System.NotImplementedException()
        };
    }
}
