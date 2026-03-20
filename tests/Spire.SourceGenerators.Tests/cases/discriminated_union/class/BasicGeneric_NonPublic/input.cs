using Spire;

namespace MyApp
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
}
