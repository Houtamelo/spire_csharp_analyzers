using Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] static partial void Ident(string name);
        [Variant] static partial void Number(int value);
        [Variant] static partial void Eof();
    }
}
