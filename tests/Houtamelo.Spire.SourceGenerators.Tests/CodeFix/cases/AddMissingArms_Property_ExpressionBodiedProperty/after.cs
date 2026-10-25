using Houtamelo.Spire;
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
            { kind: Flag.Kind.On, on: { level: var l  } } => l,
            { kind: Flag.Kind.Off, off: { reason: var reason  } } => throw new System.NotImplementedException()
        };
    }
}
