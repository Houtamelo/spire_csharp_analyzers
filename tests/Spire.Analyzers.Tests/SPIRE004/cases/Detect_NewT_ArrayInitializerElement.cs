//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is used as an array initializer element.
public class Detect_NewT_ArrayInitializerElement
{
    public void Method()
    {
        var arr = new MustInitNoCtor[]
        {
            new MustInitNoCtor(1, "a"),
            new MustInitNoCtor(), //~ ERROR
        };
    }
}
