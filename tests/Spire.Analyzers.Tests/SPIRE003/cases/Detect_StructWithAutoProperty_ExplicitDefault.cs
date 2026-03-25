//@ should_fail
// Ensure that SPIRE003 IS triggered when default(EnforceInitializationStructWithAutoProperty) is used in a local variable.
public class Detect_StructWithAutoProperty_ExplicitDefault
{
    public void Method()
    {
        var s = default(EnforceInitializationStructWithAutoProperty); //~ ERROR
    }
}
