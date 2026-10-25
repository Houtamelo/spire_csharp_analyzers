//@ should_fail
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct EventUnion
    {
        [Variant] public static partial EventUnion Click(int buttonIndex);
        [Variant] public static partial EventUnion KeyPress(char keyChar);
    }
    class CReadonly
    {
        int Test(EventUnion e) => e switch
        {
            { kind: EventUnion.Kind.Click, click: { buttonIndex: string bad } } => 1, //~ ERROR
            { kind: EventUnion.Kind.KeyPress, keyPress: { keyChar: char k } } => (int)k,
        };
    }
}
