using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Direction
    {
        [Variant] public static partial Direction North(int steps);
        [Variant] public static partial Direction South(int steps);
    }

    class Outer
    {
        class Inner
        {
            int Test(Direction d) => d switch
            {
                { kind: Direction.Kind.North, steps: var s } => s,
            };
        }
    }
}
