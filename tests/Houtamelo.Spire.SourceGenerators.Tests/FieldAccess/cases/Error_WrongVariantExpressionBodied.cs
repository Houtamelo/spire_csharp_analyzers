//@ should_fail
// SPIRE013: expression-bodied method with switch — wrong field in one arm
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Event
    {
        [Variant] public static partial Event Click(int buttonIndex);
        [Variant] public static partial Event KeyPress(char keyChar);
    }
    class C
    {
        string Describe(Event e) => e switch
        {
            (Event.Kind.Click, _) => $"key={e.keyChar}", //~ ERROR
            _ => "other",
        };
    }
}
