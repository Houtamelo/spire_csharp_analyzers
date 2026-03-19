using Spire;

namespace MyApp
{
    [DiscriminatedUnion]
    internal partial class Status
    {
        public partial class Active : Status { }
        public partial class Inactive : Status
        {
            public string Reason { get; }
            public Inactive(string reason) { Reason = reason; }
        }
    }
}
