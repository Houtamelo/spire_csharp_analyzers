//@ should_fail
// Switch statement missing Err variant
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial class Result<T, E>
    {
        public partial class Ok : Result<T, E>
        {
            public T Value { get; }
            public Ok(T value) { Value = value; }
        }
        public partial class Err : Result<T, E>
        {
            public E Error { get; }
            public Err(E error) { Error = error; }
        }
    }
    class C
    {
        void Test(Result<int, string> r)
        {
            switch (r) //~ ERROR
            {
                case Result<int, string>.Ok:
                    break;
            }
        }
    }
}
