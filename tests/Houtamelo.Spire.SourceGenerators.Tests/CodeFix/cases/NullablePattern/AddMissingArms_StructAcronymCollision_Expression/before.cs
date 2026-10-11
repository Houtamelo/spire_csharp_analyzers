#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command HTTPResponse(int value);
        [Variant] public static partial Command URL(string value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.HTTPResponse, int value) => value.ToString(),
        };

        public static Command ValueInput() => Command.URL("url");

        public static string CaptureText(string value) => value;
    }
}
