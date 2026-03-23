using Spire;

namespace Commands
{
    [DiscriminatedUnion]
    public partial class Command
    {
        public sealed partial class Start : Command
        {
            public string Name { get; }
            public Start(string name) { Name = name; }
        }
        public sealed partial class Stop : Command { }
    }
}
