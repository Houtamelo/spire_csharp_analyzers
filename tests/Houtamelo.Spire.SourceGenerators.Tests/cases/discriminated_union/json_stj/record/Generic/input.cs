using Spire;

namespace TestNs;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
public partial record Option<T>
{
    public partial record Some(T Value) : Option<T>;
    public partial record None() : Option<T>;
}
