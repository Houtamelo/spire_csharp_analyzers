//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is passed as a named method argument.
public class Detect_NewT_NamedArgument
{
    private static void Consume(MustInitNoCtor value) { }

    public void Method()
    {
        Consume(value: new MustInitNoCtor()); //~ ERROR
    }
}
