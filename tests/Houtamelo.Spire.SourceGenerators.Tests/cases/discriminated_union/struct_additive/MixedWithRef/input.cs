using Houtamelo.Spire;

namespace TestNs;

[DiscriminatedUnion(Layout.Additive)]
partial struct Event
{
    [Variant] public static partial Event Click(int x, int y, string target);
    [Variant] public static partial Event Error(string message, System.Exception ex);
    [Variant] public static partial Event Ping();
}
