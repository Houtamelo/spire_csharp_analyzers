//@ should_fail
// SPIRE014: property getter accesses variant field with no kind guard
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Packet
    {
        [Variant] public static partial Packet Data(byte[] payload);
        [Variant] public static partial Packet Ack(int sequenceNum);
    }
    class Wrapper
    {
        private Packet _packet;
        public byte[] Payload => _packet.payload; //~ ERROR
    }
}
