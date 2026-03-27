//@ not_exhaustive
// (Vehicle, bool) — missing Bike cases
#nullable enable
public class TypeHierarchyInTupleMissing
{
    public int Test(Vehicle v, bool b) => (v, b) switch
    {
        (Car, true) => 1,
        (Car, false) => 2,
    };
}
