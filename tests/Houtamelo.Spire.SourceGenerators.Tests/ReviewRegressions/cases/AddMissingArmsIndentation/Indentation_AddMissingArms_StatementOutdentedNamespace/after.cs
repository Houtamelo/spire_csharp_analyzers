using Houtamelo.Spire;

namespace Test
{
[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Move(float speed);
    [Variant] public static partial Command Pause(int ticks);
    [Variant] public static partial Command Stop();
}

class Consumer
{
    int Match(Command command)
    {
        switch (command)
        {
            case { kind: Command.Kind.Move, move: { speed: var speed } }:
                return (int)speed;
            case { kind: Command.Kind.Pause, pause: { ticks: var ticks } }:
                throw new System.NotImplementedException();
            case { kind: Command.Kind.Stop }:
                throw new System.NotImplementedException();
        }

        return -1;
    }
}
}
