//@ should_fail
// Ensure that SPIRE003 IS triggered when default is an element in a List<EnforceInitializationStruct> collection initializer.
public class Detect_DefaultLiteral_CollectionInitializerElement
{
    public void Method()
    {
        var list = new List<EnforceInitializationStruct> { default }; //~ ERROR
    }
}
