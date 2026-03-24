//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called on a MustInitEnumNoZero[] and no zero-valued member exists.
public class Detect_EnumArrayClear_NoZero
{
    public void Method()
    {
        var arr = new[] { MustInitEnumNoZero.Active, MustInitEnumNoZero.Inactive, MustInitEnumNoZero.Pending };
        Array.Clear(arr); //~ ERROR
    }
}
