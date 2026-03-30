using Houtamelo.Spire;

namespace TestNs;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
public partial record Option<T>
{
    public partial record Some(T Value) : Option<T>;
    public partial record None() : Option<T>;
}
