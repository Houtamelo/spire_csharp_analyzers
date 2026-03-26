using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Val
    {
        [Variant] public static partial Val Int(int n);
        [Variant] public static partial Val Str(string s);
    }

    class Consumer
    {
        int Test(Val v) => v switch
        {
            { kind: Val.Kind.Int, n: var bad } => 1,
            { kind: Val.Kind.Str, s: var x } => 2,
        };
    }
}
