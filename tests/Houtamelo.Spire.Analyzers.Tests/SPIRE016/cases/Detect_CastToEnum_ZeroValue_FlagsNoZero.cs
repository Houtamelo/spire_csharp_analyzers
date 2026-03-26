//@ should_fail
// Ensure that SPIRE016 IS triggered when (FlagsNoZero)0 is cast, and 0 is not a named member.
public class Detect_CastToEnum_ZeroValue_FlagsNoZero
{
    public void Method()
    {
        FlagsNoZero x = (FlagsNoZero)0; //~ ERROR
    }
}
