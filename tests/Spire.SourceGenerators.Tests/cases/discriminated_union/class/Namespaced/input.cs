using Spire;

namespace Deep.Nested.Namespace
{
    [DiscriminatedUnion]
    public partial class Token
    {
        public partial class Ident : Token
        {
            public string Name { get; }
            public Ident(string name) { Name = name; }
        }
        public partial class Number : Token
        {
            public int Value { get; }
            public Number(int value) { Value = value; }
        }
    }
}
