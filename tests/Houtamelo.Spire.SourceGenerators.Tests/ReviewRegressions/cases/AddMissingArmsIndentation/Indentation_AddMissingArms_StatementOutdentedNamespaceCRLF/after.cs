using Houtamelo.Spire;

namespace Test
{
[DiscriminatedUnion]
partial struct Message
{
    [Variant] public static partial Message Ping(int sequence);
    [Variant] public static partial Message Close();
    [Variant] public static partial Message Flush(string reason);
}

class Consumer
{
    void Handle(Message message)
    {
        switch (message)
        {
            case { kind: Message.Kind.Ping, ping: { sequence: var sequence } }:
                _ = sequence;
                break;
            case { kind: Message.Kind.Close }:
                throw new System.NotImplementedException();
            case { kind: Message.Kind.Flush, flush: { reason: var reason } }:
                throw new System.NotImplementedException();
        }
    }
}
}
