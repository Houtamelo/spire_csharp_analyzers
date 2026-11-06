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
        switch (state) {
            case { IsIdle: true }:
                throw new System.NotImplementedException();
            case { IsMoving: true, moving: var (destination, marker) }:
                throw new System.NotImplementedException();
            case { IsSpeaking: true, speaking: var (text, timer) }:
                throw new System.NotImplementedException();
        }
    }
}
