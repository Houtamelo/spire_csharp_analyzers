//@ should_fail
// Missing Err variant
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial class Result<T, E>
    {
        partial class Ok : Result<T, E>
        {
            public T Value { get; }
            public Ok(T value) { Value = value; }
        }
        partial class Err : Result<T, E>
        {
            public E Error { get; }
            public Err(E error) { Error = error; }
        }
    }
    class C
    {
        int Test(Result<int, string> r) => r switch //~ ERROR
        {
            Result<int, string>.Ok { Value: var v } => v,
        };
    }
}
