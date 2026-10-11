#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Ready();
        [Variant] public static partial Command Count(int value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command)
        {
            var value = "outer";
            return command switch
            {
                { kind: Command.Kind.Ready } => value,
                { kind: Command.Kind.Count, value: var value1 } => throw new System.NotImplementedException()
            };
        }

        public static Command ValueInput() => Command.Count(42);

        public static string CaptureInt(int value) => value.ToString();
    }
}
