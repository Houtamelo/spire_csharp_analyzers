//@ should_pass
// No diagnostics: struct with 5 variant methods is valid
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Identifier(string name);
        [Variant] public static partial Token Number(long value);
        [Variant] public static partial Token StringLiteral(string text);
        [Variant] public static partial Token Operator(char symbol);
        [Variant] public static partial Token EndOfFile();
    }
}
