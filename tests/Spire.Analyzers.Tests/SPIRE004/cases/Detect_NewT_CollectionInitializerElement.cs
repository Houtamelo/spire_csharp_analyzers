//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is used as a collection initializer element.
public class Detect_NewT_CollectionInitializerElement
{
    public void Method()
    {
        var list = new List<MustInitNoCtor>
        {
            new MustInitNoCtor(1, "a"),
            new MustInitNoCtor(), //~ ERROR
        };
    }
}
