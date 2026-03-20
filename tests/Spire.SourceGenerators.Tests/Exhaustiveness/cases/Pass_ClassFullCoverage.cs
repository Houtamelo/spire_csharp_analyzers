//@ should_pass
// All class variants covered
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
        int Test(Result<int, string> r) => r switch
        {
            Result<int, string>.Ok => 1,
            Result<int, string>.Err => 2,
        };
    }
}
