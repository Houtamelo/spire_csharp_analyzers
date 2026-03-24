//@ should_fail
// Ensure that SPIRE006 IS triggered when span.Clear() is called on a Span<MustInitEnumNoZero> and no zero-valued member exists.
public class Detect_EnumSpanClear_NoZero
{
    public void Method()
    {
        var arr = new[] { MustInitEnumNoZero.Active, MustInitEnumNoZero.Inactive, MustInitEnumNoZero.Pending };
        Span<MustInitEnumNoZero> span = arr;
        span.Clear(); //~ ERROR
    }
}
