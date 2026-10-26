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
        }
    }
}
}
