using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Event
    {
        [Variant] public static partial Event Click(int x);
        [Variant] public static partial Event KeyPress(char key);
    }

    class Consumer
    {
        int Process(Event e)
        {
            int Classify(Event ev) => ev switch
            {
                { kind: Event.Kind.Click, x: var c } => c,
                { kind: Event.Kind.KeyPress, key: char key } => throw new System.NotImplementedException()
            };
            return Classify(e);
        }
    }
}
