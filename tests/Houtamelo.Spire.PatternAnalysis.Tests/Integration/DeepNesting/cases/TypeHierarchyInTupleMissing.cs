//@ not_exhaustive
// (Vehicle, bool) — missing both Bike cases
#nullable enable
public class TypeHierarchyInTupleMissing
{
    public int Test(Vehicle v, bool b) => (v, b) switch
    {
        (Car, true) => 1,
        (Car, false) => 2,
        //~ (Bike, true)
        //~ (Bike, false)
    };
}
