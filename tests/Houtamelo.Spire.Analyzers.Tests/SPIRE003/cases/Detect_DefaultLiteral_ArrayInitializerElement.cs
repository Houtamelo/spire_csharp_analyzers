//@ should_fail
// Ensure that SPIRE003 IS triggered when default is an element in a EnforceInitializationStruct[] array initializer.
public class Detect_DefaultLiteral_ArrayInitializerElement
{
    public void Method()
    {
        EnforceInitializationStruct[] arr = new EnforceInitializationStruct[] { default }; //~ ERROR
    }
}
