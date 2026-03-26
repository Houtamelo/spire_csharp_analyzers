using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Switch
    {
        [Variant] public static partial Switch Enabled(int priority);
        [Variant] public static partial Switch Disabled(string cause);
    }

    class Consumer
    {
        Switch _switch;
        int Value => _switch switch
        {
            (Switch.Kind.Enabled, var p) => p,
            (Switch.Kind.Disabled, string cause) => throw new System.NotImplementedException()
        };
    }
}
