//@ should_fail
// Ensure that SPIRE003 IS triggered when MustInitStructWithAutoProperty s = default; is used.
public class Detect_StructWithAutoProperty_DefaultLiteral
{
    public void Method()
    {
        MustInitStructWithAutoProperty s = default; //~ ERROR
    }
}
