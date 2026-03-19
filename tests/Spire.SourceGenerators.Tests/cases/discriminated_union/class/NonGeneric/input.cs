using Spire;

namespace MyApp
{
    [DiscriminatedUnion]
    public partial class Command
    {
        public partial class Start : Command { }
        public partial class Stop : Command
        {
            public string Reason { get; }
            public Stop(string reason) { Reason = reason; }
        }
    }
}
