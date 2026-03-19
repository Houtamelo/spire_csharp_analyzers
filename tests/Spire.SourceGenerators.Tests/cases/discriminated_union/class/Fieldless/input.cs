using Spire;

namespace MyApp
{
    [DiscriminatedUnion]
    public partial class Option<T>
    {
        public partial class Some : Option<T>
        {
            public T Value { get; }
            public Some(T value) { Value = value; }
        }
        public partial class None : Option<T> { }
    }
}
