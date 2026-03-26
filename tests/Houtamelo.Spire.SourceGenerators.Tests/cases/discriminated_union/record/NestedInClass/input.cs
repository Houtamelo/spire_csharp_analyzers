using Spire;

namespace TestNs
{
    public partial class Container
    {
        [DiscriminatedUnion]
        public partial record Option<T>
        {
            public partial record Some(T Value) : Option<T>;
            public partial record None() : Option<T>;
        }
    }
}
