//@ not_exhaustive
// Single-member enum with when guard makes it non-exhaustive
public class SingleMemberEnumGuarded
{
    public int Test(OneCase c) => c switch
    {
        OneCase.Only when DateTime.Now.Ticks > 0 => 1,
        //~ OneCase.Only
    };
}
