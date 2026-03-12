//@ should_fail
// Ensure that SPIRE003 IS triggered when default(MustInitStructWithAutoProperty) is used in a local variable.
public class Detect_StructWithAutoProperty_ExplicitDefault
{
    public void Method()
    {
        var s = default(MustInitStructWithAutoProperty); //~ ERROR
    }
}
