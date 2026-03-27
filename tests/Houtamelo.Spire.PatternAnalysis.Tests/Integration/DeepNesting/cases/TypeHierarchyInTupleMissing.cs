//@ not_exhaustive
// (Vehicle, bool) — missing all Bike cases (checker reports as one composite partition)
#nullable enable
public class TypeHierarchyInTupleMissing
{
    public int Test(Vehicle v, bool b) => (v, b) switch
    {
        (Car, true) => 1,
        (Car, false) => 2,
        //~ (Bike, _)
    };
}
