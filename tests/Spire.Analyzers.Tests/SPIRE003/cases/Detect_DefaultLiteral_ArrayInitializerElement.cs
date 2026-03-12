//@ should_fail
// Ensure that SPIRE003 IS triggered when default is an element in a MustInitStruct[] array initializer.
public class Detect_DefaultLiteral_ArrayInitializerElement
{
    public void Method()
    {
        MustInitStruct[] arr = new MustInitStruct[] { default }; //~ ERROR
    }
}
