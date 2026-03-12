//@ should_fail
// Ensure that SPIRE003 IS triggered when default is an element in a List<MustInitStruct> collection initializer.
public class Detect_DefaultLiteral_CollectionInitializerElement
{
    public void Method()
    {
        var list = new List<MustInitStruct> { default }; //~ ERROR
    }
}
