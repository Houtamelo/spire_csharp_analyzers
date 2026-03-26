//@ should_fail
// Ensure that SPIRE016 IS triggered when casting a non-constant variable to a [Flags] enum — non-constant casts are still flagged
public class Detect_CastToFlags_Variable_NonConstant
{
    void M(int variable)
    {
        FlagsNoZero f = (FlagsNoZero)variable; //~ ERROR
        _ = f;
    }
}
