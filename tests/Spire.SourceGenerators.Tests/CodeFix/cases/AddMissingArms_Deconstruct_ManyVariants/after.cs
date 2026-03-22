using Spire;
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
            (Cmd.Kind.Start, var d) => 1,
            (Cmd.Kind.Pause, var dur) => 3,
            (Cmd.Kind.Stop, _) => throw new System.NotImplementedException(),
            (Cmd.Kind.Resume, _) => throw new System.NotImplementedException(),
            (Cmd.Kind.Reset, string reason) => throw new System.NotImplementedException()
        };
    }
}
