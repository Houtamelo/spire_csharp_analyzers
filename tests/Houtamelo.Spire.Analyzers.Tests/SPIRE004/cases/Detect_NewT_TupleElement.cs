//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is used as a tuple element.
public class Detect_NewT_TupleElement
{
    public (int, EnforceInitializationNoCtor) Method()
    {
        return (42, new EnforceInitializationNoCtor()); //~ ERROR
    }
}
