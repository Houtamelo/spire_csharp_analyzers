//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)(PlainEnum.A) is cast — PlainEnum.A = 0, not a member of StatusNoZero.
public class Detect_EnumToEnumCast_InvalidValue
{
    public StatusNoZero Method()
    {
        return (StatusNoZero)(PlainEnum.A); //~ ERROR
    }
}
