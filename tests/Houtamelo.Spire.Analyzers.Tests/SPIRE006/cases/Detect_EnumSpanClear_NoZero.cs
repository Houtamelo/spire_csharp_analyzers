//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<EnforceInitializationEnumNoZero> and no zero-valued member exists.
public class Detect_EnumSpanClear_NoZero
{
    public void Method()
    {
        var arr = new[] { EnforceInitializationEnumNoZero.Active, EnforceInitializationEnumNoZero.Inactive, EnforceInitializationEnumNoZero.Pending };
        Span<EnforceInitializationEnumNoZero> span = arr;
        span.Clear(); //~ ERROR
    }
}
