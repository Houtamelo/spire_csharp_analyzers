//@ should_fail
// Ensure that SPIRE016 IS triggered when a non-constant PlainEnum variable is cast to StatusNoZero.
public class Detect_EnumToEnumCast_Variable
{
    public StatusNoZero Method(PlainEnum p)
    {
        return (StatusNoZero)p; //~ ERROR
    }
}
