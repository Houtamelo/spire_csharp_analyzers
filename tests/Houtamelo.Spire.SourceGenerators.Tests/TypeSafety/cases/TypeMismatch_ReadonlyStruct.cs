//@ should_fail
using Spire;
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
            (EventUnion.Kind.Click, string bad) => 1, //~ ERROR
            (EventUnion.Kind.KeyPress, char k) => (int)k,
        };
    }
}
