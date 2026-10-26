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
  };
}
}
