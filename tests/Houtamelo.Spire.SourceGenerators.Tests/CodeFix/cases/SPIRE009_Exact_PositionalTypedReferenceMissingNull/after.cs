#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct State
{
    [Variant] public static partial State Speaking(string? text, float timer);
    [Variant] public static partial State Idle();
}

class Consumer
{
    static int Read(State state) => state switch
    {
        { IsSpeaking: true, speaking: (string text, _) } => 1,
        { IsIdle: true } => 2,
        { IsSpeaking: true, speaking: { text: null } } => throw new System.NotImplementedException(),
    };
}
