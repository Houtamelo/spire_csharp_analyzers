//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear is called on a MustInitEnumWithZero[] because None=0 is a valid named member.
public class NoReport_EnumArrayClear_WithZeroMember
{
    public void Method()
    {
        var arr = new[] { MustInitEnumWithZero.Active, MustInitEnumWithZero.Inactive, MustInitEnumWithZero.None };
        Array.Clear(arr);
    }
}
