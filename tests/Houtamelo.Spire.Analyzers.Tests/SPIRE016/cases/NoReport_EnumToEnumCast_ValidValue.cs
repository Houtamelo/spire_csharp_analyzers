//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting between enums where the source value matches a target named member.
public class NoReport_EnumToEnumCast_ValidValue
{
    public StatusNoZero Method()
    {
        // PlainEnum.B = 1, StatusNoZero.Active = 1 — valid
        return (StatusNoZero)(PlainEnum.B);
    }
}
