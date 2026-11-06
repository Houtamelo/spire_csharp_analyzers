using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct State
{
    [Variant] public static partial State Idle();
    [Variant] public static partial State Moving(int destination, string marker);
    [Variant] public static partial State Speaking(string text, float timer);
}

class Consumer
{
    static void Tick(State state)
    {
        switch (state) {}
    }
}
