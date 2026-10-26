using Houtamelo.Spire;

namespace Test
{
[DiscriminatedUnion]
partial struct Command
{
  [Variant] public static partial Command Move(float speed);
  [Variant] public static partial Command Stop();
}

class Consumer
{
  int Match(Command command) => command switch
  {
    { kind: Command.Kind.Move, move: { speed: var speed } } => throw new System.NotImplementedException(),
    { kind: Command.Kind.Stop } => throw new System.NotImplementedException()
  };
}
}
