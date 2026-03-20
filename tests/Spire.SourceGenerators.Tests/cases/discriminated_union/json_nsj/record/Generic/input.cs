using Spire;

namespace TestNs;

[DiscriminatedUnion(Json = JsonLibrary.NewtonsoftJson)]
public partial record Option<T>
{
    public partial record Some(T Value) : Option<T>;
    public partial record None() : Option<T>;
}
