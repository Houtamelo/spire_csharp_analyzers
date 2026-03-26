using Houtamelo.Spire;
using System.Threading.Tasks;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Msg
    {
        [Variant] public static partial Msg Ping(int seq);
        [Variant] public static partial Msg Pong(int seq);
        [Variant] public static partial Msg Close();
    }

    class Consumer
    {
        async Task<int> Test(Msg m)
        {
            await Task.Delay(1);
            return m switch
            {
                { kind: Msg.Kind.Ping, seq: var s } => s,
            };
        }
    }
}
