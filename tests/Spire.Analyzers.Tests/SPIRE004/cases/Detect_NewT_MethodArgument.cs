//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as a positional method argument.
public class Detect_NewT_MethodArgument
{
    private static void Consume(EnforceInitializationNoCtor value) { }

    public void Method()
    {
        Consume(new EnforceInitializationNoCtor()); //~ ERROR
    }
}
