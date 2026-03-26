using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Flag
    {
        [Variant] public static partial Flag On(int level);
        [Variant] public static partial Flag Off(string reason);
    }

    class Consumer
    {
        Flag _flag;
        int Value => _flag switch
        {
            { kind: Flag.Kind.On, level: var l } => l,
        };
    }
}
