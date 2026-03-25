//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called on a EnforceInitializationEnumNoZero[] and no zero-valued member exists.
public class Detect_EnumArrayClear_NoZero
{
    public void Method()
    {
        var arr = new[] { EnforceInitializationEnumNoZero.Active, EnforceInitializationEnumNoZero.Inactive, EnforceInitializationEnumNoZero.Pending };
        Array.Clear(arr); //~ ERROR
    }
}
