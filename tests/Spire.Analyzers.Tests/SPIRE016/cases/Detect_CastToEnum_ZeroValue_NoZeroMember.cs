//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)0 is cast, and 0 is not a named member.
public class Detect_CastToEnum_ZeroValue_NoZeroMember
{
    public StatusNoZero Method()
    {
        return (StatusNoZero)0; //~ ERROR
    }
}
