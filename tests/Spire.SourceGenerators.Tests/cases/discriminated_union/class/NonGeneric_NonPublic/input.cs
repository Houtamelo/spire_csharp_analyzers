using Spire;

namespace MyApp
{
    [DiscriminatedUnion]
    partial class Command
    {
        partial class Start : Command { }
        partial class Stop : Command
        {
            public string Reason { get; }
            public Stop(string reason) { Reason = reason; }
        }
    }
}
