//@ exhaustive
// Guarded arm comes first but unguarded arms still cover all; guard is ignored by analysis
public class GuardedArms_RedundantGuard
{
    public int Test(bool b) => b switch
    {
        true when DateTime.Now.Ticks > 0 => 99,
        true => 1,
        false => 2,
    };
}
