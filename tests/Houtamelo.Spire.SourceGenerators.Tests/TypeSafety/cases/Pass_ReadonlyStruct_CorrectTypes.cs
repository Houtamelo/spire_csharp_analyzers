//@ should_pass
using Houtamelo.Spire;
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
            { kind: EventPass.Kind.Click, click: { clickIndex: int idx } } => idx,
            { kind: EventPass.Kind.KeyPress, keyPress: { pressedChar: char k } } => (int)k,
        };
    }
}
