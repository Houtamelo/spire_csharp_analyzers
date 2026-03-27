//@ exhaustive
// (Vehicle, bool) — type hierarchy in tuple position
#nullable enable
public class TypeHierarchyInTuple
{
    public int Test(Vehicle v, bool b) => (v, b) switch
    {
        (Car, true) => 1,
        (Car, false) => 2,
        (Bike, _) => 3,
    };
}
