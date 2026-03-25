//@ should_fail
// Ensure that SPIRE003 IS triggered when EnforceInitializationStructWithAutoProperty s = default; is used.
public class Detect_StructWithAutoProperty_DefaultLiteral
{
    public void Method()
    {
        EnforceInitializationStructWithAutoProperty s = default; //~ ERROR
    }
}
