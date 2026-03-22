//@ should_fail
// SPIRE_DU004: layout parameter ignored for class union
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    public abstract partial class NetworkMessage //~ ERROR
    {
        public sealed partial class Connect : NetworkMessage { public string Host { get; } public Connect(string h) => Host = h; }
        public sealed partial class Disconnect : NetworkMessage { public int Code { get; } public Disconnect(int c) => Code = c; }
    }
}
