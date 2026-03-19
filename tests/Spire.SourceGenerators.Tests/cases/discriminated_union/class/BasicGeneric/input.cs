using Spire;

namespace MyApp
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
}
