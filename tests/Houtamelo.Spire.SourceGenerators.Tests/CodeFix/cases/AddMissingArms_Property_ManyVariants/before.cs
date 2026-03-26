using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Cmd
    {
        [Variant] public static partial Cmd Start(int delay);
        [Variant] public static partial Cmd Stop();
        [Variant] public static partial Cmd Pause(double duration);
        [Variant] public static partial Cmd Resume();
        [Variant] public static partial Cmd Reset(string reason);
    }

    class Consumer
    {
        int Test(Cmd c) => c switch
        {
            { kind: Cmd.Kind.Start, delay: var d } => 1,
            { kind: Cmd.Kind.Pause, duration: var dur } => 3,
        };
    }
}
