#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Count(int value);
        [Variant] public static partial Command Text(string value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.Count, int value) => value.ToString(),
        };

        public static Command ValueInput() => Command.Text("text");

        public static string CaptureText(string value) => value;
    }
}
