using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Number(double value);
        [Variant] public static partial Token Text(string content);
    }

    class Consumer
    {
        Token _token;

        int Tag => _token switch
        {
            { kind: Token.Kind.Number, value: var bad } => 1,
            { kind: Token.Kind.Text, content: var x } => 2,
        };
    }
}
