//@ not_exhaustive
// Guarded true arm is excluded; unguarded false is present but true is missing
public class GuardedArms_NotCounted
{
    public int Test(bool b) => b switch
    {
        true when DateTime.Now.Ticks > 0 => 1,
        false => 2,
    };
}
