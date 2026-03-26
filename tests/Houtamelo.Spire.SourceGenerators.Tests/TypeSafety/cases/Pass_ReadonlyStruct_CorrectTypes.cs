//@ should_pass
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct EventPass
    {
        [Variant] public static partial EventPass Click(int clickIndex);
        [Variant] public static partial EventPass KeyPress(char pressedChar);
    }
    class CReadonlyPass
    {
        int Test(EventPass e) => e switch
        {
            (EventPass.Kind.Click, int idx) => idx,
            (EventPass.Kind.KeyPress, char k) => (int)k,
        };
    }
}
