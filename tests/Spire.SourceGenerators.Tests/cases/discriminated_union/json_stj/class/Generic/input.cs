using Spire;

namespace TestNs;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
public partial class Result<T, E>
{
    public partial class Ok(T Value) : Result<T, E>;
    public partial class Err(E Error) : Result<T, E>;
}
