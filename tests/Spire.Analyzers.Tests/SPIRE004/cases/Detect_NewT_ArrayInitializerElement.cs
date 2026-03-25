//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is used as an array initializer element.
public class Detect_NewT_ArrayInitializerElement
{
    public void Method()
    {
        var arr = new EnforceInitializationNoCtor[]
        {
            new EnforceInitializationNoCtor(1, "a"),
            new EnforceInitializationNoCtor(), //~ ERROR
        };
    }
}
