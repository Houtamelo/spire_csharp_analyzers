//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is used as a tuple element.
public class Detect_NewT_TupleElement
{
    public (int, MustInitNoCtor) Method()
    {
        return (42, new MustInitNoCtor()); //~ ERROR
    }
}
