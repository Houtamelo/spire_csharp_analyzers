//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as a named method argument.
public class Detect_NewT_NamedArgument
{
    private static void Consume(EnforceInitializationNoCtor value) { }

    public void Method()
    {
        Consume(value: new EnforceInitializationNoCtor()); //~ ERROR
    }
}
