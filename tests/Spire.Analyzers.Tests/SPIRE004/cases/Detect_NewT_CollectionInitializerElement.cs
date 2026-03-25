//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is used as a collection initializer element.
public class Detect_NewT_CollectionInitializerElement
{
    public void Method()
    {
        var list = new List<EnforceInitializationNoCtor>
        {
            new EnforceInitializationNoCtor(1, "a"),
            new EnforceInitializationNoCtor(), //~ ERROR
        };
    }
}
